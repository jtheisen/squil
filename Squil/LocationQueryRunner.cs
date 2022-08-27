﻿using System.Collections.Specialized;
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
        public RelatedEntities RootRelation { get; set; }
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

            var isRoot = table == null;

            var cmTable = isRoot ? context.CircularModel.RootTable : context.CircularModel.GetTable(parser.Parse(table));

            var extentFactory = new ExtentFactory(2);

            var cmIndex = index?.Apply(i => cmTable.Indexes.Get(i, $"Could not find index '{index}' in table '{table}'"));

            var extentOrder = cmIndex?.Columns.Select(c => c.c.Name).ToArray();
            var extentValues = cmIndex?.Columns.Select(c => (String)query[c.c.Name]).TakeWhile(c => c != null).ToArray();

            var isSingletonQuery = table == null || (cmIndex != null && cmIndex.IsUnique && extentValues?.Length == extentOrder?.Length);

            var extent = extentFactory.CreateRootExtent(
                cmTable,
                isSingletonQuery ? ExtentFlavorType.PageList : ExtentFlavorType.BlockList,
                cmIndex, extentOrder, extentValues
                );

            using var connection = context.GetConnection();

            var (entity, sql, resultXml) = context.QueryGenerator.Query(connection, extent);

            return new LocationQueryResult
            {
                QueryType = table == null ? QueryControllerQueryType.Root : isSingletonQuery ? QueryControllerQueryType.Single : (extentValues?.Length ?? 0) == 0 ? QueryControllerQueryType.Table : QueryControllerQueryType.Sublist,
                RootName = connectionName,
                RootUrl = $"/query/{connectionName}",
                Sql = sql,
                Entity = entity,
                RootRelation = isRoot ? null : entity.Related.Single()
            };
        }
    }
}
