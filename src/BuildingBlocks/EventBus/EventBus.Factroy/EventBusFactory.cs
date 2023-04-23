using EventBus.Base;
using EventBus.Base.Abstraction;
using EventBus.RabbitMQ;
using EvetBus.AzureServiceBus;

namespace EventBus.Factroy
{
    public static class EventBusFactory
    {
        public static IEventBus Create(EventBusConfig config, IServiceProvider serviceProvider)
        {
            return config.EventBusType switch
            {
                EventBusType.AzureServiceBus => new EventBusServiceBus(config, serviceProvider),
                _ => new EventBusRabbitMQ(config, serviceProvider),
            };
        }
    }
}