namespace AzureEventGridSimulator.Controllers
{
    using System.Linq;
    using AzureEventGridSimulator.Domain.Services;
    using AzureEventGridSimulator.Infrastructure.Settings;

    using Microsoft.AspNetCore.Mvc;

    [Route("api/[controller]")]
    [ApiController]
    public class EventHistoryController : SimulatorController
    {
        private readonly EventHistory _eventStore;

        public EventHistoryController(SimulatorSettings simulatorSettings, EventHistory eventStore)
            : base(simulatorSettings)
        {
            _eventStore = eventStore;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return new OkObjectResult(_eventStore.GetAll(Topic).Select(x => x.Event));
        }

        [Route("withresults")]
        [HttpGet]
        public IActionResult GetWithResults()
        {
            return new OkObjectResult(_eventStore.GetAll(Topic));
        }
    }
}
