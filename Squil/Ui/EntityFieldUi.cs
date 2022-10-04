namespace Squil;

public enum ColumnRenderClass
{
    None,
    PrimaryName,
    PrimaryKey,
    Data,
    Text
}

public enum StringLengthClass
{
    NoString,
    Short,
    Medium,
    Large
}

public class EntityFieldUi
{
    public Entity Entity { get; set; }

    public CMTable Table { get; set; }
}

public class EntityRelationFieldUi : EntityFieldUi
{
    public RelatedEntities RelatedEntities { get; set; }
}

public class EntityColumnFieldUi : EntityFieldUi
{
    public CMColumn Column { get; set; }

    public ColumnRenderClass RenderClass { get; set; }

    public StringLengthClass StringLengthClass { get; set; }
}

public static class UiExtensions
{
    public static String GetCssClass(this StringLengthClass slc) => slc switch
    {
        StringLengthClass.NoString => null,
        _ => slc.ToString().ToLower()
    };
}