using Squil.SchemaBuilding;
using System.Collections.Specialized;
using System.Dynamic;
using System.Web;
using TaskLedgering;

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

        public Int32? ListLimit { get; set; }

        public NameValueCollection KeyParams { get; }
        public NameValueCollection RestParams { get; }
        public NameValueCollection SearchValues { get; }

        public LocationQueryRequest(NameValueCollection queryParams, NameValueCollection searchValues)
        {
            (var keyParams, var restParams) = SplitParams(queryParams);

            KeyParams = keyParams;
            RestParams = restParams;
            SearchValues = searchValues;
        }

        static (NameValueCollection keyParams, NameValueCollection restParams) SplitParams(NameValueCollection queryParams)
        {
            var groups =
                from key in queryParams.AllKeys
                group key by key?.StartsWith('$') ?? false into g
                select g;

            var keyParams = (
                from g in groups.Where(g => g.Key == true)
                from key in g
                select (key[1..], queryParams[key])
            ).ToMap().ToNameValueCollection();

            var restParams = (
                from g in groups.Where(g => g.Key == false)
                from key in g
                select (key, queryParams[key])
            ).ToMap().ToNameValueCollection();

            return (keyParams, restParams);
        }
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
        public IEnumerable<LedgerEntry> LedgerEntries { get; set; }
    }

    public enum CanLoadMoreStatus
    {
        Unavailable,
        Can,
        Did,
        Complete
    }

    public class LocationQueryVm
    {
        public LocationQueryRequest LastRequest { get; private set; }
        
        public LocationQueryResult Result { get; }
        public LocationQueryResult LastResult { get; private set; }

        public Entity Entity { get; private set; }

        public Int32 KeyValuesCount { get; }

        public LocationQueryVm(LocationQueryRequest request, LocationQueryResult result)
        {
            LastRequest = request;
            Result = result;

            var relation = Result.RootRelation;

            if (relation?.Extent.Flavor.type == ExtentFlavorType.Block)
            {
                var table = relation.RelationEnd.Table;

                var keyKeysArray = request.KeyParams.Cast<String>().ToArray();

                KeyValuesCount = keyKeysArray.Length;

                var accountedIndexes = new HashSet<ObjectName>();

                Indexes = table.Indexes.Values
                    .Where(i => i.IsSupported)
                    .StartsWith(keyKeysArray)
                    .Where(i => i.Columns.Length > keyKeysArray.Length)
                    .Select(i => new IndexVm { Index = i, IsCurrent = request.Index == i.Name })
                    .ToArray();

                accountedIndexes.AddRange(Indexes.Select(i => i.Index.ObjectName));

                var unsupportedGroups = (
                    from i in table.Indexes.Values
                    where !i.IsSupported
                    group i by i.UnsupportedReason into g
                    select new UnsuitableIndexesVm(g.Key, from i in g select new IndexVm { Index = i, UnsupportedReason = g.Key })
                ).ToList();

                accountedIndexes.AddRange(from i in table.Indexes.Values where !i.IsSupported select i.ObjectName);

                var unsuitableReason = new CsdUnsupportedReason("Prefix mismatch", "Although this index is supported on the table, you can't use it to search within the subset you're looking at.", "");

                var unsuitableIndexes = (
                    from i in table.Indexes.Values
                    where !accountedIndexes.Contains(i.ObjectName)
                    select new IndexVm { Index = i, UnsupportedReason = unsuitableReason }
                ).ToArray();

                if (unsuitableIndexes.Any())
                {
                    unsupportedGroups.Insert(0, new UnsuitableIndexesVm(unsuitableReason, unsuitableIndexes));
                }

                UnsuitableIndexes = unsupportedGroups.ToArray();

                CurrentIndex = Indexes.FirstOrDefault(i => i.IsCurrent);
            }

            Update(request, result);
        }

        public void Update(LocationQueryRequest request, LocationQueryResult result)
        {
            LastRequest = request;
            LastResult = result;

            if (LastResult.IsValidationOk)
            {
                Entity = LastResult.Entity;
            }

            if (CurrentIndex != null)
            {
                CurrentIndex.ValidatedValues = result.ValidatedColumns;
            }
        }

        public static readonly Int32 LoadMoreLimit = 10;

        public CanLoadMoreStatus CanLoadMore()
        {
            var r = LastResult.RootRelation;

            if (r == null) return CanLoadMoreStatus.Unavailable;

            if (r.Extent.Flavor.type != ExtentFlavorType.Block) return CanLoadMoreStatus.Unavailable;

            if (LastRequest.ListLimit.HasValue) return CanLoadMoreStatus.Did;

            return r.Extent.Limit == r.List.Length ? CanLoadMoreStatus.Can : CanLoadMoreStatus.Complete;
        }

        public IndexVm CurrentIndex { get; }

        public IndexVm[] Indexes { get; }

        public UnsuitableIndexesVm[] UnsuitableIndexes { get; set; }
    }

    public record UnsuitableIndexesVm(CsdUnsupportedReason Reason, IEnumerable<IndexVm> Indexes);

    public class IndexVm
    {
        public ValidationResult[] ValidatedValues { get; set; }

        public CMIndexlike Index { get; set; }

        public Boolean IsCurrent { get; set; }

        public CsdUnsupportedReason UnsupportedReason { get; set; }
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
            using var ledger = InstallTaskLedger();

            var context = connections.GetContext(connectionName);

            var table = request.Table;
            var index = request.Index;

            var isRoot = table == null;

            var cmTable = isRoot ? context.CircularModel.RootTable : context.CircularModel.GetTable(parser.Parse(table));

            var extentFactory = new ExtentFactory(2);

            var cmIndex = index?.Apply(i => cmTable.Indexes.Get(i, $"Could not find index '{index}' in table '{table}'"));

            ValidationResult GetColumnValue(CMDirectedColumn column)
            {
                var keyValue = request.KeyParams[column.Name];
                var searchValue = request.SearchValues[column.Name];

                var validationResult = column.c.Type.Validate(keyValue, searchValue, column.d, default);

                return validationResult;
            }

            var columnValues = cmIndex?.Columns.Select(GetColumnValue).ToArray();

            var keyValueCount = columnValues?.TakeWhile(cv => cv.IsKeyValue).Count();

            var extentOrder = cmIndex?.Columns.Select(c => c.Name).ToArray();
            var extentValues = columnValues?.TakeWhile(cv => cv.SqlLowerValue != null).Select(cv => cv.GetSqlValue()).ToArray();

            var isSingletonQuery = table == null || (cmIndex != null && cmIndex.IsUnique && extentValues?.Length == extentOrder?.Length && columnValues.All(v => v.IsKeyValue));

            var extent = extentFactory.CreateRootExtent(
                cmTable,
                isSingletonQuery ? ExtentFlavorType.PageList : ExtentFlavorType.BlockList,
                cmIndex, extentOrder, extentValues, keyValueCount, request.ListLimit
                );

            var result = new LocationQueryResult
            {
                QueryType = table == null ? QueryControllerQueryType.Root : isSingletonQuery ? QueryControllerQueryType.Single : (extentValues?.Length ?? 0) == 0 ? QueryControllerQueryType.Table : QueryControllerQueryType.Sublist,
                RootName = connectionName,
                RootUrl = $"/query/{connectionName}",
                IsRoot = isRoot,
                ValidatedColumns = columnValues,
                IsValidationOk = columnValues?.All(r => r.IsOk) ?? true,
                LedgerEntries = ledger.GetEntries()
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
