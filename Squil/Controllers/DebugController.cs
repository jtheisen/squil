using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Squil.Controllers
{
    [Route("debug")]
    [ApiController]
    public class DebugController : Controller
    {
        private readonly ILogger<DebugController> _logger;

        public DebugController(ILogger<DebugController> logger)
        {
            _logger = logger;
        }

        [HttpGet("ip")]
        public Task<String> GetIp()
        {
            var client = new HttpClient();
            return client.GetStringAsync("https://api.ipify.org?format=json");
        }
    }
}
