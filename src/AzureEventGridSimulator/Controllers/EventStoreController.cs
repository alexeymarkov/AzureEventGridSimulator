namespace AzureEventGridSimulator.Controllers
{
    using AzureEventGridSimulator.Domain.Services;

    using Microsoft.AspNetCore.Mvc;

    [Route("api/[controller]")]
    [ApiController]
    public class EventStoreController : ControllerBase
    {
        private readonly EventStore _eventStore;

        public EventStoreController(EventStore eventStore)
        {
            _eventStore = eventStore;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return new OkObjectResult(_eventStore.GetAll());
        }

        [HttpGet("{id}")]
        public IActionResult Get(string id)
        {
            var eventEntity = _eventStore.FinById(id);
            if (eventEntity == null)
            {
                return new NotFoundResult();
            }

            return new OkObjectResult(eventEntity);
        }
    }
}
