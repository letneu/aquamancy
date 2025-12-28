using Microsoft.AspNetCore.Mvc;
using Aquamancy.Logic;

namespace Aquamancy.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ApiController : ControllerBase
    {
        public record PostParams(string MachineName, string Temperature);

        private readonly ITemperatureReadingLogic _temperatureReadingLogic;

        public ApiController(ITemperatureReadingLogic temperatureReadingLogic)
        {
            _temperatureReadingLogic = temperatureReadingLogic;
        }

        [HttpPost("submit")]
        public async Task<IActionResult> Submit([FromBody] PostParams data)
        {
            if (data is null)
                return BadRequest("Missing body");

            if (string.IsNullOrWhiteSpace(data.MachineName))
                return BadRequest("Missing MachineName");

            if (string.IsNullOrWhiteSpace(data.Temperature))
                return BadRequest("Missing Temperature");

            var result = await _temperatureReadingLogic.Insert(data);

            return Ok(new { isTooHot = result.Temperature > result.Probe.MaxTemperature, isTooCold = result.Temperature < result.Probe.MinTemperature, SendFrequencyInSeconds = result.Probe.SendFrequencyInSeconds });
        }
    }
}