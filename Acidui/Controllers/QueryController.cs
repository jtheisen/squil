using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;



namespace Acidui.Controllers
{
    [Route("query")]
    public class QueryController : Controller
    {
        private readonly AciduiContext context;

        public QueryController(AciduiContext context)
        {
            this.context = context;
        }

        public class IndexVm
        {
            public String Html { get; set; }
        }

        [HttpGet("{table}")]
        public IActionResult Index(String table)
        {
            var cmTable = context.CircularModel.GetTable(table);

            var extentFactory = new ExtentFactory();

            var extent = extentFactory.CreateRootExtent(cmTable);

            using var connection = context.GetConnection();

            var result = context.QueryGenerator.Query(connection, extent);

            var renderer = new HtmlRenderer();

            var html = renderer.RenderToHtml(result.Related.Single());

            return View(new IndexVm { Html = html });
        }
    }
}
