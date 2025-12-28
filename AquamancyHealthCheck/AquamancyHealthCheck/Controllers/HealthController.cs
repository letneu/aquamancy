using Microsoft.AspNetCore.Mvc;

namespace AquamancyHealthCheck.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HealthController : ControllerBase
    {
        [HttpGet("Ping")]
        public bool Ping()
        {
            HealthCheckService.LastHealthCheckTime = DateTime.UtcNow;
            return true;
        }
    }
}
