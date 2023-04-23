using EventBus.Base.Events;

namespace EventBust.UnitTest.Events.Events;

public class OrderCreatedIntegrationEvent : IntegrationEvent
{
    public OrderCreatedIntegrationEvent(int id)
    {
        Id = id;
    }

    public int Id { get; set; }
}