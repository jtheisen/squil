using System;
using System.Collections.Generic;
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

        [HttpGet("{table}/{index}")]
        public IActionResult Index(String table, String index = null)
        {
            var cmTable = context.CircularModel.GetTable(table);

            var extentFactory = new ExtentFactory();

            var extent = extentFactory.CreateExtent(cmTable);

            if (index != null)
            {
                // We're using keys for the time being.
                var cmKey = cmTable.DomesticKeys.Get(index, $"Could not find index '{index}' in table '{table}'");

                extent.Order = cmKey.Columns.Select(c => c.Name).ToArray();
                extent.Values = cmKey.Columns.Select(c => (String)Request.Query[c.Name]).TakeWhile(c => c != null).ToArray();
            }

            using var connection = context.GetConnection();

            var result = context.QueryGenerator.Query(connection, extent);

            var renderer = new HtmlRenderer();

            var html = renderer.RenderToHtml(result.Related.Single());

            return View(new IndexVm { Html = html });
        }
    }
}
