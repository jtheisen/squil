using System.Collections.Specialized;
using System.Web;

namespace Squil
{
    public enum QueryControllerQueryType
    {
        Root,
        Single,
        Table,
        Sublist
    }

    public class LocationQueryResult
    {
        public QueryControllerQueryType QueryType { get; set; }
        public String RootUrl { get; set; }
        public String RootName { get; set; }
        public String Sql { get; set; }
        public Entity Entity { get; set; }
        public String Html { get; set; }
    }

    public class LocationQueryRunner
    {
        private readonly ObjectNameParser parser = new ObjectNameParser();
        private readonly ConnectionManager connections;

        public LocationQueryRunner(ConnectionManager connections)
        {
            this.connections = connections;
        }

        public LocationQueryResult Query(String connectionName, String table, String index, NameValueCollection query)
        {
            var context = connections.GetContext(connectionName);

            var cmTable = table?.Apply(t => context.CircularModel.GetTable(parser.Parse(t))) ?? context.CircularModel.RootTable;

            var extentFactory = new ExtentFactory(2);

            // We're using keys for the time being.
            var cmKey = index?.Apply(i => cmTable.Keys.Get(i, $"Could not find index '{index}' in table '{table}'"));

            var extentOrder = cmKey?.Columns.Select(c => c.Name).ToArray();
            var extentValues = cmKey?.Columns.Select(c => (String)query[c.Name]).TakeWhile(c => c != null).ToArray();

            var isSingletonQuery = cmKey != null && cmKey is CMDomesticKey && extentValues?.Length == extentOrder?.Length;

            var extent = extentFactory.CreateRootExtent(
                cmTable,
                isSingletonQuery ? ExtentFlavorType.PageList : ExtentFlavorType.BlockList,
                extentOrder, extentValues
                );

            using var connection = context.GetConnection();

            var (entity, sql, resultXml) = context.QueryGenerator.Query(connection, extent);

            var renderer = new HtmlRenderer(rest => $"/query/{connectionName}/{rest}");

            var html = renderer.RenderToHtml(entity);

            return new LocationQueryResult
            {
                QueryType = table == null ? QueryControllerQueryType.Root : isSingletonQuery ? QueryControllerQueryType.Single : (extentValues?.Length ?? 0) == 0 ? QueryControllerQueryType.Table : QueryControllerQueryType.Sublist,
                RootName = connectionName,
                RootUrl = $"/query/{connectionName}",
                Sql = sql,
                Entity = entity,
                Html = html
            };
        }
    }
}
