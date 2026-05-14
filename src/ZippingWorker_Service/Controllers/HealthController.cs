using Microsoft.AspNetCore.Mvc;

namespace ZippingWorker_Service.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        /// <summary>
        /// Simple ping endpoint to check if the server is responsive
        /// </summary>
        /// <returns>Returns "pong" text response</returns>
        [HttpGet("ping")]
        [Produces("text/plain")]
        public IActionResult Ping()
        {
            return Ok("pong");
        }
    }
}
