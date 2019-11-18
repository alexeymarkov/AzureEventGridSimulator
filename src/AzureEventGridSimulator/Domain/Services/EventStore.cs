namespace AzureEventGridSimulator.Domain.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using AzureEventGridSimulator.Domain.Entities;

    public class EventStore
    {
        private readonly List<EventGridEvent> _events = new List<EventGridEvent>();

        public IEnumerable<EventGridEvent> GetAll()
        {
            return _events;
        }

        public EventGridEvent FinById(string id)
        {
            return _events.FirstOrDefault(x => x.Id == id);
        }

        public void Add(EventGridEvent[] events)
        {
            _events.AddRange(events);
        }
    }
}
