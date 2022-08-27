using static System.Web.HttpUtility;

namespace Squil
{
    public class QueryContext
    {
        public UrlRenderer RenderUrl { get; }

        public QueryContext(UrlRenderer renderUrl)
        {
            RenderUrl = renderUrl;
        }

        public String RenderEntityUrl(CMTable table, Entity entity)
        {
            if (table == null) return null;

            return table.PrimaryKey?.Apply(k => RenderEntityUrl(entity, k));
        }

        public String RenderEntityUrl(Entity entity, CMIndexlike key)
        {
            var keyPart = entity != null && key != null
                ? $"/{key.Name}?" + String.Join("&", key.Columns.Select(c => $"{c.c.Name}={entity.ColumnValues[c.c.Name]}"))
                : "";

            var url = RenderUrl($"{key.Table.Name.Escaped}{keyPart.TrimEnd('?')}");

            return url;
        }

        String RenderColumnTupleUrl(
            CMTable table,
            CMColumnTuple columnTuple,
            CMColumnTuple columns,
            IDictionary<String, String> columnValueSource
        )
        {
            var queryPart = "";

            var columnValues = columns.Columns
                .Select(c => columnValueSource.GetOrDefault(c.c.Name))
                .TakeWhile(c => c != null);

            if (columnValues.Any())
            {
                queryPart = "?" + String.Join("&", columns.Columns
                    .Zip(columnValues, (ic, cv) => $"{UrlEncode(ic.c.Name)}={UrlEncode(cv)}")
                );
            }

            var subNamePart = columnTuple != null ? $"/{UrlEncode(columnTuple.Name)}" : "";

            var url = RenderUrl($"{table.Name.Escaped}{subNamePart}{queryPart}");

            return url;
        }

        public String RenderEntitiesUrl(Entity parentEntity, RelatedEntities relatedEntities)
        {
            if (parentEntity == null || !relatedEntities.RelationEnd.IsMany) return null;

            var end = relatedEntities.RelationEnd;

            var index = relatedEntities.ChooseIndex();

            // We're allowing no index if we're looking at an entire table
            if (end.Key.Name != "" && index == null) return null;

            return RenderColumnTupleUrl(end.Table, index, end.Key, parentEntity.ColumnValues);
        }

        public String RenderIndexUrl(CMIndexlike index, IDictionary<String, String> columnValueSource)
        {
            return RenderColumnTupleUrl(index.Table, index, index, columnValueSource);
        }
    }
}
