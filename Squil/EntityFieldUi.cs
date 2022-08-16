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

        public String RenderEntityUrl(Entity entity, CMKey key)
        {
            var keyPart = entity != null && key != null
                ? $"/{key.Name}?" + String.Join("&", key.Columns.Select(c => $"{c.Name}={entity.ColumnValues[c.Name]}"))
                : "";

            var url = RenderUrl($"{key.Table.Name.Escaped}{keyPart.TrimEnd('?')}");

            return url;
        }
    }

    public class EntityFieldUi
    {
        public Entity Entity { get; set; }

        public CMTable Table { get; set; }
    }

    public class EntityRelationFieldUi : EntityFieldUi
    {
        public RelatedEntities RelatedEntites { get; set; }
    }

    public class EntityColumnFieldUi : EntityFieldUi
    {
        public CMColumn Column { get; set; }

        public ColumnRenderClass RenderClass { get; set; }
    }
}
