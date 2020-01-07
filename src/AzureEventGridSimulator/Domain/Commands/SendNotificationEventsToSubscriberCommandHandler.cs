using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Domain.Commands
{
    public class SendNotificationEventsToSubscriberCommandHandler : AsyncRequestHandler<SendNotificationEventsToSubscriberCommand>
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger _logger;
        private readonly EventHistory _eventStore;

        public SendNotificationEventsToSubscriberCommandHandler(IHttpClientFactory httpClientFactory, ILogger logger, EventHistory eventStore)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _eventStore = eventStore;
        }

        protected override async Task Handle(SendNotificationEventsToSubscriberCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("{EventCount} event(s) received on topic '{TopicName}'", request.Events.Length, request.Topic.Name);

            foreach (var eventGridEvent in request.Events)
            {
                eventGridEvent.Topic = $"/subscriptions/{Guid.Empty:D}/resourceGroups/eventGridSimulator/providers/Microsoft.EventGrid/topics/{request.Topic.Name}";
                eventGridEvent.MetadataVersion = "1";
            }

            var sentEvents = await Task.WhenAll(request.Events.Select(async evt => new EventHistory.EventHistoryRecord
            {
                Event = evt,
                ExecutionResults = await Task.WhenAll(request.Topic.Subscribers.Select(async subscription =>
                {
                    var (response, exception) = await SendToSubscriber(subscription, evt);
                    return new EventHistory.EventExecutionResult
                    {
                        Subscription = subscription.Name,
                        ResponseStatusCode = (int?)response?.StatusCode,
                        ResponseReasonPhrase = response?.ReasonPhrase,
                        ResponseBody = response?.Content != null ? await response.Content.ReadAsStringAsync() : null,
                        Exception = exception?.ToString()
                    };
                }))
            }));

            _eventStore.Add(request.Topic, sentEvents);
        }

        private async Task<(HttpResponseMessage Response, HttpRequestException Exception)> SendToSubscriber(SubscriptionSettings subscription, EventGridEvent evt)
        {
            try
            {
                if (!subscription.DisableValidation &&
                    subscription.ValidationStatus != SubscriptionValidationStatus.ValidationSuccessful)
                {
                    _logger.LogWarning("Subscription '{SubscriberName}' can't receive events. It's still pending validation.", subscription.Name);
                    return (null, null);
                }

                _logger.LogDebug("Sending to subscriber '{SubscriberName}'.", subscription.Name);

                var result = new Dictionary<EventGridEvent, (HttpResponseMessage Response, HttpRequestException Exception)>();

                // "Event Grid sends the events to subscribers in an array that has a single event. This behavior may change in the future."
                // https://docs.microsoft.com/en-us/azure/event-grid/event-schema
                if (subscription.Filter.AcceptsEvent(evt))
                {
                    var json = JsonConvert.SerializeObject(new[] { evt }, Formatting.Indented);
                    using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                    {
                        var httpClient = _httpClientFactory.CreateClient();
                        httpClient.DefaultRequestHeaders.Add("aeg-event-type", "Notification");
                        httpClient.Timeout = TimeSpan.FromSeconds(15);

                        try
                        {
                            var response = await httpClient.PostAsync(subscription.Endpoint, content);

                            if (response.IsSuccessStatusCode)
                            {
                                _logger.LogDebug("Event {EventId} sent to subscriber '{SubscriberName}' successfully.", evt.Id, subscription.Name);
                            }
                            else
                            {
                                _logger.LogError(
                                    "Failed to send event {EventId} to subscriber '{SubscriberName}', '{TaskStatus}', '{Reason}'.",
                                        evt.Id,
                                        subscription.Name,
                                        response.StatusCode.ToString(),
                                        response.ReasonPhrase);
                            }

                            return (response, null);
                        }
                        catch (HttpRequestException e)
                        {
                            _logger.LogError(
                                e,
                                "Failed to send event {EventId} to subscriber '{SubscriberName}'.",
                                evt.Id,
                                subscription.Name);

                            return (null, e);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Event {EventId} filtered out for subscriber '{SubscriberName}'.", evt.Id, subscription.Name);
                }

                return (null, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send to subscriber '{SubscriberName}'.", subscription.Name);
                return (null, null);
            }
        }
    }
}
