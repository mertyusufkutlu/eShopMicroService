using System.Text;
using EventBus.Base;
using EventBus.Base.Events;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Management;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace EvetBus.AzureServiceBus;

public class EventBusServiceBus : BaseEventBus
{
    private ITopicClient _topicClient;
    private ManagementClient _managementClient;
    private readonly ILogger _logger;

    public EventBusServiceBus(IServiceProvider serviceProvider, EventBusConfig config, ITopicClient topicClient,
        ManagementClient managementClient, ILogger logger) : base(serviceProvider, config)
    {
        _logger = (serviceProvider.GetService(typeof(ILogger<EventBusServiceBus>)) as ILogger<EventBusServiceBus>)!;
        _managementClient = new ManagementClient(config.EventBusConnectionString);
        _topicClient = CreateTopicClient();
    }

    private ITopicClient CreateTopicClient()
    {
        if (_topicClient == null || _topicClient.IsClosedOrClosing)
        {
            _topicClient = new TopicClient(EventBusConfig.EventBusConnectionString, EventBusConfig.DefaultTopicName,
                RetryPolicy.Default);
        }

        //Ensure that topic already exist
        if (!_managementClient.TopicExistsAsync(EventBusConfig.DefaultTopicName).GetAwaiter().GetResult())
            _managementClient.CreateTopicAsync(EventBusConfig.DefaultTopicName).GetAwaiter().GetResult();

        return _topicClient;
    }

    public override void Publish(IntegrationEvent @event)
    {
        var eventName = @event.GetType().Name; // Example : OrderCreatedIntegrationEvent

        eventName = ProcessEventName(eventName); // Example : OrderCreated

        var eventStr = JsonConvert.SerializeObject(@event);
        var bodyArray = Encoding.UTF8.GetBytes(eventStr);

        var message = new Message()
        {
            MessageId = Guid.NewGuid().ToString(),
            Body = bodyArray,
            Label = eventName
        };
        _topicClient.SendAsync(message).GetAwaiter().GetResult();
        ;
    }

    public override void Subscribe<T, TH>()
    {
        var eventName = typeof(T).Name;
        eventName = ProcessEventName(eventName);

        if (!SubsManager.HasSubscriptionsForEvent(eventName))
        {
            var subClient = CreateSubClientIfNotExist(eventName);

            RegisterSubscriptionClientMessageHandler(subClient);
        }

        _logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TH).Name);
        SubsManager.AddSubscription<T, TH>();
    }

    public override void UnSubscribe<T, TH>()
    {
        var eventName = typeof(T).Name;

        try
        {
            var subClient = CreateSubClient(eventName);

            subClient.RemoveRuleAsync(eventName).GetAwaiter().GetResult();
        }
        catch (MessagingEntityNotFoundException)
        {
            _logger.LogWarning("The messaging entity {eventName} Could not be found", eventName);
        }

        _logger.LogInformation("Unsubscribing from event {EventName}", eventName);

        SubsManager.RemoveSubscription<T, TH>();
    }

    private void RegisterSubscriptionClientMessageHandler(ISubscriptionClient subscriptionClient)
    {
        subscriptionClient.RegisterMessageHandler(
            async (message, token) =>
            {
                var eventName = $"{message.Label}";
                var messageData = Encoding.UTF8.GetString(message.Body);

                if (await ProcessEvent(ProcessEventName(eventName), messageData))
                {
                    await subscriptionClient.CompleteAsync(message.SystemProperties.LockToken);
                }
            },
            new MessageHandlerOptions(ExceptionReceivedHandler) { MaxConcurrentCalls = 10, AutoComplete = false });
    }

    private Task ExceptionReceivedHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
    {
        var ex = exceptionReceivedEventArgs.Exception;
        var context = exceptionReceivedEventArgs.ExceptionReceivedContext;

        _logger.LogError(ex, "ERROR handling message: {ExceptionsMessage} - Context: {@ExceptionContext}", ex.Message,
            context);
        return Task.CompletedTask;
    }

    private ISubscriptionClient CreateSubClientIfNotExist(String eventName)
    {
        var subClient = CreateSubClient(eventName);

        var exist = _managementClient.SubscriptionExistsAsync(EventBusConfig.DefaultTopicName, GetSubName(eventName))
            .GetAwaiter().GetResult();

        if (!exist)
        {
            _managementClient.CreateSubscriptionAsync(EventBusConfig.DefaultTopicName, GetSubName(eventName))
                .GetAwaiter();
            RemoveDefaultRule(subClient);
        }

        CreateRuleIfNotExist(ProcessEventName(eventName), subClient);
        return subClient;
    }

    private void CreateRuleIfNotExist(string eventName, ISubscriptionClient subscriptionClient)
    {
        bool ruleExist;
        try
        {
            var rule = _managementClient.GetRuleAsync(EventBusConfig.DefaultTopicName, eventName, eventName)
                .GetAwaiter().GetResult();
            ruleExist = rule != null;
        }
        catch (MessagingEntityNotFoundException)
        {
            ruleExist = false;
        }

        if (!ruleExist)
        {
            subscriptionClient.AddRuleAsync(new RuleDescription
            {
                Filter = new CorrelationFilter { Label = eventName },
                Name = eventName,
            }).GetAwaiter().GetResult();
        }
    }

    private void RemoveDefaultRule(SubscriptionClient subscriptionClient)
    {
        try
        {
            subscriptionClient.RemoveRuleAsync(RuleDescription.DefaultRuleName).GetAwaiter().GetResult();
            ;
        }
        catch (Exception e)
        {
            _logger.LogWarning("The Message entity {DefaultRuleName} Could not be found",
                RuleDescription.DefaultRuleName);
        }
    }

    private SubscriptionClient CreateSubClient(string eventName)
    {
        return new SubscriptionClient(EventBusConfig.EventBusConnectionString, EventBusConfig.DefaultTopicName,
            GetSubName(eventName));
    }

    public override void Dispose()
    {
        base.Dispose();
        _topicClient.CloseAsync().GetAwaiter().GetResult();
        _managementClient.CloseAsync().GetAwaiter().GetResult();
        _topicClient = null;
        _managementClient = null;
    }
}