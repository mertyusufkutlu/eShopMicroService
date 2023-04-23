using EventBus.Base.Abstraction;
using EventBust.UnitTest.Events.Events;

namespace EventBust.UnitTest.Events.EventHandlers;

public class OrderCreatedIntegrationEventHandler : IIntegrationEventHandler<OrderCreatedIntegrationEvent>
{
    public Task Handle(OrderCreatedIntegrationEvent @event)
    {
        //
        return Task.CompletedTask;
    }
}