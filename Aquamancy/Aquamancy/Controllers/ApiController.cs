using Microsoft.AspNetCore.Mvc;
using Aquamancy.ILogic;

namespace Aquamancy.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ApiController(ITemperatureReadingLogic temperatureReadingLogic, IErrorTriggerLogic errorTriggerLogic) : ControllerBase
    {
        public record PostParams(string MachineName, string Temperature, int Rssi, bool FirstLoop);

        private readonly ITemperatureReadingLogic _temperatureReadingLogic = temperatureReadingLogic;
        private readonly IErrorTriggerLogic _errorTriggerLogic = errorTriggerLogic;

        [HttpPost("submit")]
        public async Task<IActionResult> Submit([FromBody] PostParams data)
        {
            try
            {
                if (data is null)
                {
                    throw new BadHttpRequestException("Missing body");
                }

                if (string.IsNullOrWhiteSpace(data.MachineName))
                {
                    throw new BadHttpRequestException("Missing MachineName");
                }

                if (string.IsNullOrWhiteSpace(data.Temperature))
                {
                    throw new BadHttpRequestException("Missing Temperature");
                }

                var result = await _temperatureReadingLogic.Insert(data);

                return Ok(new { isTooHot = result.Temperature > result.Probe.MaxTemperature, isTooCold = result.Temperature < result.Probe.MinTemperature, SendFrequencyInSeconds = result.Probe.SendFrequencyInSeconds });
            }
            catch (Exception ex)
            {
                _errorTriggerLogic.TriggerError(ex, "Error in ApiController.Submit");

                if(ex is BadHttpRequestException)
                {
                    return BadRequest(ex.Message);
                }

                return StatusCode(500, ex.Message);
            }
        }
    }
}