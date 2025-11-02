using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hermes.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly ILogger<HealthController> _logger;

        public HealthController(ILogger<HealthController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Health check endpoint. Returns HTTP200 OK if the service is running.
        /// </summary>
        /// <returns>HTTP200 OK response.</returns>
        [HttpGet]
        public IActionResult Health()
        {
            _logger.LogInformation("[{ClassName}] Entry: Health endpoint called.", nameof(HealthController));

            return Ok("Healthy");
        }
    }
}
