using System.Diagnostics;
using EventBus.Base.Abstraction;
using EventBus.Base.SubManagers;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace EventBus.Base.Events;

public abstract class BaseEventBus : IEventBus
{
    public readonly IServiceProvider ServiceProvider;
    public readonly IEventBusSubscriptionManager SubsManager;
    public EventBusConfig EventBusConfig { get; set; }

    protected BaseEventBus(IServiceProvider serviceProvider, EventBusConfig config)
    {
        EventBusConfig = config;
        ServiceProvider = serviceProvider;
        SubsManager = new InMemoryEventBusSubscriptionManager(ProcessEventName);
    }


    public virtual string ProcessEventName(string eventName)
    {
        if (EventBusConfig.DeleteEventPrefix)
            eventName = eventName.TrimStart(EventBusConfig.EventNamePrefix.ToArray());
        if (EventBusConfig.DeleteEventSuffix)
            eventName = eventName.TrimStart(EventBusConfig.EventNameSuffix.ToArray());

        return eventName;
    }

    public virtual string GetSubName(string eventName)
    {
        return $"{EventBusConfig.SubscriberClientAppName}.{ProcessEventName(eventName)}";
    }

    public virtual void Dispose()
    {
        EventBusConfig = null!;
        SubsManager.Clear();
    }

    public async Task<bool> ProcessEvent(string eventName, string message)
    {
        eventName = ProcessEventName(eventName);

        var process = false;

        if (SubsManager.HasSubscriptionsForEvent(eventName))
        {
            var subs = SubsManager.GetHandlersForEvent(eventName);
            using (var scope = ServiceProvider.CreateScope())
            {
                foreach (var sub in subs)
                {
                    var handler = ServiceProvider.GetService(sub.HandlerType);
                    if (handler == null) continue;

                    var eventType =
                        SubsManager.GetEventTypeByName(
                            $"{EventBusConfig.EventNamePrefix}{eventName}{EventBusConfig.EventNameSuffix}");
                    var integrationEvent = JsonConvert.DeserializeObject(message, eventType);


                    var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
                    await ((Task)concreteType.GetMethod("Handler")!.Invoke(handler,
                        new object?[] { integrationEvent })!)!;
                }
            }

            process = true;
        }

        return process;
    }

    public abstract void Publish(IntegrationEvent @event);

    public abstract void Subscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>;

    public abstract void UnSubscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>;
}