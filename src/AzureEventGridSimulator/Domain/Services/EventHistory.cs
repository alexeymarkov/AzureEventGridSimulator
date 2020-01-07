namespace AzureEventGridSimulator.Domain.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using AzureEventGridSimulator.Domain.Entities;
    using AzureEventGridSimulator.Infrastructure.Settings;

    public class EventHistory
    {
        public class EventExecutionResult
        {
            public string Subscription { get; set; }
            public int? ResponseStatusCode { get; set; }
            public string ResponseReasonPhrase { get; set; }
            public string ResponseBody { get; set; }
            public string Exception { get; set; }
        }

        public class EventHistoryRecord
        {
            public EventGridEvent Event { get; set; }
            public IEnumerable<EventExecutionResult> ExecutionResults { get; set; }
        }

        private readonly ConcurrentDictionary<TopicSettings, List<EventHistoryRecord>> _events = new ConcurrentDictionary<TopicSettings, List<EventHistoryRecord>>();

        public IEnumerable<EventHistoryRecord> GetAll(TopicSettings topic)
        {
            if (_events.TryGetValue(topic, out var events))
            {
                return events;
            }

            return Enumerable.Empty<EventHistoryRecord>();
        }

        public void Add(TopicSettings topic, IEnumerable<EventHistoryRecord> events)
        {
            _events.AddOrUpdate(topic, events.ToList(), (key, existingEvents) =>
            {
                existingEvents.AddRange(events);
                return existingEvents;
            });
        }
    }
}
