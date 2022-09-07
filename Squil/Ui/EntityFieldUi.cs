namespace Squil
{
    public enum ColumnRenderClass
    {
        None,
        PrimaryName,
        PrimaryKey,
        Data
    }

    public delegate String UrlRenderer(String rest);

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
