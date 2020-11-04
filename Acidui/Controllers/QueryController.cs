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

        [HttpGet("{table?}/{index?}")]
        public IActionResult Index(String table = "", String index = null)
        {
            var cmTable = context.CircularModel.GetTable(table);

            var extentFactory = new ExtentFactory();

            // We're using keys for the time being.
            var cmKey = index?.Apply(i => cmTable.Keys.Get(i, $"Could not find index '{index}' in table '{table}'"));

            var extentOrder = cmKey?.Columns.Select(c => c.Name).ToArray();
            var extentValues = cmKey?.Columns.Select(c => (String)Request.Query[c.Name]).TakeWhile(c => c != null).ToArray();

            var isSingletonQuery = cmKey != null && extentValues?.Length == extentOrder?.Length;

            var extent = extentFactory.CreateRootExtent(cmTable, isSingletonQuery ? ExtentFlavorType.PageList : ExtentFlavorType.BlockList);

            extent.Order = extentOrder;
            extent.Values = extentValues;

            using var connection = context.GetConnection();

            var result = context.QueryGenerator.Query(connection, extent);

            var renderer = new HtmlRenderer();

            var html = renderer.RenderToHtml(result);

            return View(new IndexVm { Html = html });
        }
    }
}
