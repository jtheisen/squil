using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace Squil.Controllers
{
    [Route("query")]
    public class QueryController : Controller
    {
        private readonly ObjectNameParser parser = new ObjectNameParser();
        private readonly ConnectionManager connections;

        public QueryController(ConnectionManager connections)
        {
            this.connections = connections;
        }

        public class IndexVm
        {
            public String Html { get; set; }
        }

        [HttpGet("{connectionName}/{table?}/{index?}")]
        public IActionResult Index(String connectionName, String table = null, String index = null)
        {
            var context = connections.GetContext(connectionName);

            var cmTable = table?.Apply(t => context.CircularModel.GetTable(parser.Parse(t))) ?? context.CircularModel.RootTable;

            var extentFactory = new ExtentFactory(2);

            // We're using keys for the time being.
            var cmKey = index?.Apply(i => cmTable.Keys.Get(i, $"Could not find index '{index}' in table '{table}'"));

            var extentOrder = cmKey?.Columns.Select(c => c.Name).ToArray();
            var extentValues = cmKey?.Columns.Select(c => (String)Request.Query[c.Name]).TakeWhile(c => c != null).ToArray();

            var isSingletonQuery = cmKey != null && cmKey is CMDomesticKey && extentValues?.Length == extentOrder?.Length;

            var extent = extentFactory.CreateRootExtent(
                cmTable,
                isSingletonQuery ? ExtentFlavorType.PageList : ExtentFlavorType.BlockList,
                extentOrder, extentValues
                );

            using var connection = context.GetConnection();

            var result = context.QueryGenerator.Query(connection, extent);

            var renderer = new HtmlRenderer(rest => $"/query/{connectionName}/{rest}");

            var html = renderer.RenderToHtml(result);

            return View(new IndexVm { Html = html });
        }
    }
}
