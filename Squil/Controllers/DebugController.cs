using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Squil.Controllers
{
    [Route("debug")]
    [ApiController]
    public class DebugController : Controller
    {
        private readonly ILogger<DebugController> _logger;
        private readonly IOptions<List<ConnectionConfiguration>> configuredConnections;

        public DebugController(ILogger<DebugController> logger, IOptions<List<ConnectionConfiguration>> configuredConnections)
        {
            _logger = logger;
            this.configuredConnections = configuredConnections;
        }

        [HttpGet("ip")]
        public Task<String> GetIp()
        {
            var client = new HttpClient();
            return client.GetStringAsync("https://api.ipify.org?format=json");
        }

        //[HttpGet("configured-connections")]
        //public String GetConfiguredConnections()
        //{
        //    return JsonConvert.SerializeObject(configuredConnections.Value, Formatting.Indented);
        //}
    }
}
