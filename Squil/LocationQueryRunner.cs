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

    public class LocationQueryRequest
    {
        public String Table { get; set; }

        public String Index { get; set; }

        public NameValueCollection KeyValues { get; set; }
        public NameValueCollection SearchValues { get; set; }
    }

    public class LocationQueryResult
    {
        public QueryControllerQueryType QueryType { get; set; }
        public String RootUrl { get; set; }
        public String RootName { get; set; }
        public String Sql { get; set; }
        public Entity Entity { get; set; }
        public Boolean IsRoot { get; set; }
        public RelatedEntities RootRelation => IsRoot ? null : Entity?.Related.Single();
        public Boolean IsValidationOk { get; set; }
        public ValidationResult[] ValidatedColumns { get; set; }
    }

    public class LocationQueryRunner
    {
        private readonly ObjectNameParser parser = new ObjectNameParser();
        private readonly ConnectionManager connections;

        public LocationQueryRunner(ConnectionManager connections)
        {
            this.connections = connections;
        }

        public LocationQueryResult Query(String connectionName, LocationQueryRequest request)
        {
            var context = connections.GetContext(connectionName);

            var table = request.Table;
            var index = request.Index;

            var isRoot = table == null;

            var cmTable = isRoot ? context.CircularModel.RootTable : context.CircularModel.GetTable(parser.Parse(table));

            var extentFactory = new ExtentFactory(2);

            var cmIndex = index?.Apply(i => cmTable.Indexes.Get(i, $"Could not find index '{index}' in table '{table}'"));

            ValidationResult GetColumnValue(CMColumn column)
            {
                var keyValue = request.KeyValues[column.Name];
                var searchValue = request.SearchValues[column.Name];

                var validationResult = column.Type.Validate(keyValue, searchValue, default);

                return validationResult;
            }

            var columnValues = cmIndex?.Columns.Select(c => c.c).Select(GetColumnValue).ToArray();

            var keyValueCount = columnValues?.TakeWhile(cv => cv.IsKeyValue).Count();

            var extentOrder = cmIndex?.Columns.Select(c => c.c.Name).ToArray();
            var extentValues = columnValues?.TakeWhile(cv => cv.SqlValue != null).Select(cv => cv.SqlValue).ToArray();

            var isSingletonQuery = table == null || (cmIndex != null && cmIndex.IsUnique && extentValues?.Length == extentOrder?.Length);

            var extent = extentFactory.CreateRootExtent(
                cmTable,
                isSingletonQuery ? ExtentFlavorType.PageList : ExtentFlavorType.BlockList,
                cmIndex, extentOrder, extentValues, keyValueCount
                );

            var result = new LocationQueryResult
            {
                QueryType = table == null ? QueryControllerQueryType.Root : isSingletonQuery ? QueryControllerQueryType.Single : (extentValues?.Length ?? 0) == 0 ? QueryControllerQueryType.Table : QueryControllerQueryType.Sublist,
                RootName = connectionName,
                RootUrl = $"/query/{connectionName}",
                IsRoot = isRoot,
                ValidatedColumns = columnValues,
                IsValidationOk = columnValues?.All(r => r.IsOk) ?? true
            };

            if (!result.IsValidationOk) return result;

            using var connection = context.GetConnection();

            var (entity, sql, resultXml) = context.QueryGenerator.Query(connection, extent);

            result.Sql = sql;
            result.Entity = entity;

            return result;
        }
    }
}
