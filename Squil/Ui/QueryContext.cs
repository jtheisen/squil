using static System.Web.HttpUtility;

namespace Squil
{
    public class QueryContext
    {
        public Boolean InDebug { get; set; }

        public UrlRenderer UrlRenderer { get; }

        public QueryContext(UrlRenderer urlRenderer)
        {
            UrlRenderer = urlRenderer;
        }

        public String RenderEntityUrl(CMTable table, Entity entity)
        {
            if (table == null) return null;

            return table.PrimaryKey?.Apply(k => RenderEntityUrl(entity, k));
        }

        public String RenderEntityUrl(Entity entity, CMIndexlike key)
        {
            var url = UrlRenderer.RenderUrl(
                new[] { key.Table.Name.Escaped, key.Name },
                from c in key.Columns select ("$", c.c.Name, entity.ColumnValues[c.c.Name])
            );

            return url;
        }

        String RenderColumnTupleUrl(
            CMTable table,
            CMColumnTuple columnsOnTarget,
            CMColumnTuple columnsOnSource,
            IDictionary<String, String> columnValueSource,
            CMIndexlike index = null
        )
        {
            var columnValues = columnsOnSource.Columns
                .Select(c => columnValueSource.GetOrDefault(c.c.Name))
                .TakeWhile(c => c != null);

            var url = UrlRenderer.RenderUrl(
                new[] { table.Name.Escaped, index?.Name },
                columnsOnTarget.Columns
                    .Zip(columnValues, (ic, cv) => ("$", ic.c.Name, cv))
            );

            return url;
        }

        public String RenderEntitiesUrl(Entity parentEntity, RelatedEntities relatedEntities)
        {
            if (parentEntity == null || !relatedEntities.RelationEnd.IsMany) return null;

            var end = relatedEntities.RelationEnd;

            var index = relatedEntities.ChooseIndex();

            // We're allowing no index if we're looking at an entire table
            if (end.Key.Name != "" && index == null) return null;

            return RenderColumnTupleUrl(end.Table, end.Key, end.OtherEnd.Key, parentEntity.ColumnValues, index);
        }

        public String RenderIndexUrl(CMIndexlike index, IDictionary<String, String> columnValueSource)
        {
            return RenderColumnTupleUrl(index.Table, index, index, columnValueSource, index);
        }
    }
}
