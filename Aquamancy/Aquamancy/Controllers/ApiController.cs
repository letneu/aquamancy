using Microsoft.AspNetCore.Mvc;
using Aquamancy.ILogic;
using Aquamancy.Dto;

namespace Aquamancy.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ApiController(IReadingLogic readingLogic, IErrorTriggerLogic errorTriggerLogic) : ControllerBase
    {
        private readonly IReadingLogic _readingLogic = readingLogic;
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


                //data = data with { Tds = null };

                var (Success, ErrorMessage, Probe, ColorR, ColorG, ColorB) = await _readingLogic.Insert(data);

                return Ok(new {  IsSuccess = Success,
                    ErrorMessage = ErrorMessage,
                    SendFrequencyInSeconds = Probe.SendFrequencyInSeconds,
                    ColorR = ColorR,
                    ColorG = ColorG,
                    ColorB = ColorB });
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