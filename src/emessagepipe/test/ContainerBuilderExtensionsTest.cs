using System;
using System.Collections.Generic;
using GdUnit4;
using MessagePipe;
using VContainer;
using static GdUnit4.Assertions;

namespace Enaweg.MessagePipe.Tests;

/// <summary>
/// Verifies that <see cref="ContainerBuilderExtensions.RegisterMessagePipe(IContainerBuilder)"/>
/// bridges MessagePipe into a VContainer scope, so publisher/subscriber pairs resolve out of the
/// same container and actually deliver messages.
/// </summary>
[TestSuite]
public class ContainerBuilderExtensionsTest
{
	[TestCase]
	public void RegisterMessagePipe_ResolvesPublisherAndSubscriber()
	{
		var builder = new ContainerBuilder();
		var options = builder.RegisterMessagePipe();
		builder.RegisterMessageBroker<string>(options);

		using var container = builder.Build();

		AssertThat(container.Resolve<IPublisher<string>>()).IsNotNull();
		AssertThat(container.Resolve<ISubscriber<string>>()).IsNotNull();
	}

	[TestCase]
	public void RegisterMessageBroker_DeliversPublishedMessageToSubscriber()
	{
		var builder = new ContainerBuilder();
		var options = builder.RegisterMessagePipe();
		builder.RegisterMessageBroker<string>(options);

		using var container = builder.Build();

		var received = new List<string>();
		using IDisposable subscription = container.Resolve<ISubscriber<string>>()
			.Subscribe(message => received.Add(message));

		container.Resolve<IPublisher<string>>().Publish("hello");

		AssertThat(received.Count).IsEqual(1);
		AssertThat(received[0]).IsEqual("hello");
	}

	[TestCase]
	public void RegisterMessageBroker_KeyedBrokerOnlyDeliversToMatchingKey()
	{
		var builder = new ContainerBuilder();
		var options = builder.RegisterMessagePipe();
		builder.RegisterMessageBroker<string, string>(options);

		using var container = builder.Build();

		var received = new List<string>();
		using IDisposable subscription = container.Resolve<ISubscriber<string, string>>()
			.Subscribe("key-a", message => received.Add(message));

		var publisher = container.Resolve<IPublisher<string, string>>();
		publisher.Publish("key-b", "ignored");
		publisher.Publish("key-a", "delivered");

		AssertThat(received.Count).IsEqual(1);
		AssertThat(received[0]).IsEqual("delivered");
	}
}
