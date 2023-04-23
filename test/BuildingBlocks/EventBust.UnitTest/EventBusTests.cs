using EventBus.Base;
using EventBus.Base.Abstraction;
using EventBus.Factroy;
using EventBust.UnitTest.Events.EventHandlers;
using EventBust.UnitTest.Events.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabbitMQ.Client;

namespace EventBust.UnitTest;

[TestClass]
public class EventBusTests
{
    private ServiceCollection services;

    public EventBusTests(ServiceCollection services)
    {
        this.services = new ServiceCollection();
        services.AddLogging(configure => configure.AddConsole());
    }

    [TestMethod]
    public void SubscribeEventOnRabbitMQTest()
    {
        services.AddSingleton<IEventBus>(sp =>
        {
            EventBusConfig config = new()
            {
                ConnectionRetryCount = 5,
                SubscriberClientAppName = "EventBusUnitTest",
                DefaultTopicName = "eShopMicroServiceTopicName",
                EventBusType = EventBusType.RabbitMQ,
                EventNameSuffix = "IntegrationEvent",
                // Connection = new ConnectionFactory()
                // {
                //     HostName = "localhost",
                //     Port = 15672,
                //     UserName = "guest",
                //     Password = "guest"
                // }
            };
            return EventBusFactory.Create(config, sp);
        });
        var serviceProvider = services.BuildServiceProvider();

        var eventBust = serviceProvider.GetRequiredService<IEventBus>();

        eventBust.Subscribe<OrderCreatedIntegrationEvent, OrderCreatedIntegrationEventHandler>();
        eventBust.UnSubscribe<OrderCreatedIntegrationEvent, OrderCreatedIntegrationEventHandler>();
    }
}