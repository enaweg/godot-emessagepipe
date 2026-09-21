using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using VContainer;

namespace Enaweg.MessagePipe.VContainer;

/// <summary>
/// Adapts an <see cref="IContainerBuilder"/> to the <see cref="IServiceCollection"/> surface that
/// MessagePipe's own registration helpers are written against, forwarding every
/// <see cref="ServiceDescriptor"/> to VContainer.
/// </summary>
/// <remarks>
/// The proxy keeps its own list of descriptors so that <see cref="IServiceCollection"/> stays
/// enumerable (MessagePipe's <c>TryAdd</c>-style helpers inspect it), but the list is a record of
/// what was forwarded — VContainer holds the authoritative registrations.
/// <para>
/// VContainer cannot un-register, so the removal members of <see cref="IList{T}"/> throw
/// <see cref="NotSupportedException"/> rather than dropping a registration that has already been
/// applied to the builder.
/// </para>
/// </remarks>
internal sealed class ContainerBuilderProxy : IServiceCollection
{
    const string RemovalNotSupported =
        "A registration cannot be removed once it has been forwarded to VContainer. "
        + "Build the service collection without the unwanted registration instead.";

    static readonly MethodInfo RegisterFactoryDefinition = GetPrivateStaticMethod(nameof(RegisterFactory));
    static readonly MethodInfo RegisterInstanceDefinition = GetPrivateStaticMethod(nameof(RegisterInstance));

    static MethodInfo GetPrivateStaticMethod(string name) =>
        typeof(ContainerBuilderProxy).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{name} could not be located by reflection.");

    readonly IContainerBuilder builder;
    readonly List<ServiceDescriptor> descriptors = new();

    public ContainerBuilderProxy(IContainerBuilder builder)
    {
        this.builder = builder;
    }

    /// <summary>Registers <paramref name="serviceType"/> as its own implementation.</summary>
    /// <param name="serviceType">The concrete type to register.</param>
    /// <param name="lifetime">The VContainer lifetime to register it with.</param>
    public void Add(Type serviceType, Lifetime lifetime)
    {
        builder.Register(serviceType, lifetime);
    }

    /// <summary>Registers <paramref name="implementationType"/> behind <paramref name="serviceType"/>.</summary>
    /// <param name="serviceType">The contract the service resolves as.</param>
    /// <param name="implementationType">The concrete type to instantiate.</param>
    /// <param name="lifetime">The VContainer lifetime to register it with.</param>
    public void Add(Type serviceType, Type implementationType, Lifetime lifetime)
    {
        builder.Register(implementationType, lifetime).As(serviceType);
    }

    /// <inheritdoc />
    public int Count => descriptors.Count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always, when setting — see the type remarks.</exception>
    public ServiceDescriptor this[int index]
    {
        get => descriptors[index];
        set => throw new NotSupportedException(RemovalNotSupported);
    }

    /// <inheritdoc />
    public void Add(ServiceDescriptor item)
    {
        if (item is null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        Register(item);
        descriptors.Add(item);
    }

    /// <inheritdoc />
    /// <remarks>
    /// VContainer has no registration ordering, so the descriptor is appended regardless of
    /// <paramref name="index"/>.
    /// </remarks>
    public void Insert(int index, ServiceDescriptor item) => Add(item);

    /// <inheritdoc />
    public bool Contains(ServiceDescriptor item) => descriptors.Contains(item);

    /// <inheritdoc />
    public void CopyTo(ServiceDescriptor[] array, int arrayIndex) => descriptors.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public int IndexOf(ServiceDescriptor item) => descriptors.IndexOf(item);

    /// <inheritdoc />
    public IEnumerator<ServiceDescriptor> GetEnumerator() => descriptors.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always — see the type remarks.</exception>
    public void Clear() => throw new NotSupportedException(RemovalNotSupported);

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always — see the type remarks.</exception>
    public bool Remove(ServiceDescriptor item) => throw new NotSupportedException(RemovalNotSupported);

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always — see the type remarks.</exception>
    public void RemoveAt(int index) => throw new NotSupportedException(RemovalNotSupported);

    void Register(ServiceDescriptor descriptor)
    {
        var lifetime = LifetimeMapping.ToVContainer(descriptor.Lifetime);

        if (descriptor.ImplementationInstance is not null)
        {
            // VContainer's RegisterInstance registers under the *static* type argument, so the
            // service type has to be supplied explicitly rather than inferred as object.
            Invoke(
                RegisterInstanceDefinition,
                RequireClosedServiceType(descriptor, "an instance"),
                builder,
                descriptor.ImplementationInstance);
            return;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            Invoke(
                RegisterFactoryDefinition,
                RequireClosedServiceType(descriptor, "a factory"),
                builder,
                descriptor.ImplementationFactory,
                lifetime);
            return;
        }

        var implementationType = descriptor.ImplementationType
            ?? throw new ArgumentException(
                $"The descriptor for {descriptor.ServiceType} has no implementation type, instance or factory.",
                nameof(descriptor));

        var typeRegistration = builder.Register(implementationType, lifetime);
        if (implementationType != descriptor.ServiceType)
        {
            typeRegistration.As(descriptor.ServiceType);
        }
    }

    /// <summary>
    /// Closes <paramref name="definition"/> over <paramref name="serviceType"/> and calls it,
    /// rethrowing whatever VContainer threw instead of a <see cref="TargetInvocationException"/>.
    /// </summary>
    static void Invoke(MethodInfo definition, Type serviceType, params object[] arguments)
    {
        try
        {
            definition.MakeGenericMethod(serviceType).Invoke(null, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
        }
    }

    static Type RequireClosedServiceType(ServiceDescriptor descriptor, string registrationKind)
    {
        if (descriptor.ServiceType.IsGenericTypeDefinition)
        {
            throw new NotSupportedException(
                $"The open generic service {descriptor.ServiceType} cannot be registered from {registrationKind}.");
        }

        return descriptor.ServiceType;
    }

    static void RegisterInstance<TService>(IContainerBuilder builder, object instance)
        where TService : class
    {
        builder.RegisterInstance((TService)instance);
    }

    static void RegisterFactory<TService>(
        IContainerBuilder builder,
        Func<IServiceProvider, object> factory,
        Lifetime lifetime)
        where TService : class
    {
        builder.Register(resolver => (TService)factory(new ObjectResolverProxy(resolver)), lifetime);
    }
}
