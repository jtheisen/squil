using System.Xml.Serialization;

namespace Squil.SchemaBuilding;

public class SysWithComment
{
    [XmlNoColumn]
    [XmlAttribute("comment")]
    public String Comment { get; set; }
}

[XmlRoot("root")]
public class SysRoot : SysWithComment
{
    [XmlArray("sys.schemas")]
    public SysSchema[] Schemas { get; set; }

    [XmlAttribute("__schema-date")]
    public DateTime SchemaDate { get; set; }
}

[XmlType("s")]
[XmlTable("schemas")]
[DebuggerDisplay("{Name}")]
public class SysSchema : SysWithComment
{
    [XmlAttribute("name")]
    public String Name { get; set; }

    [XmlAttribute("schema_id")]
    Int32 SchemaId { get; set; }

    [XmlArray("tables")]
    public SysTable[] Tables { get; set; } = Empties<SysTable>.Array;
}

[XmlType("o")]
[XmlTable("objects")]
[DebuggerDisplay("{Name}")]
public class SysObject : SysWithComment
{
    [XmlAttribute("object_id")]
    public Int32 ObjectId { get; set; }

    [XmlAttribute("schema_id")]
    Int32 SchemaId { get; set; }

    [XmlAttribute("name")]
    public String Name { get; set; }

    [XmlAttribute("type")]
    public String Type { get; set; }

    [XmlAttribute("type_desc")]
    public String TypeDesc { get; set; }

    [XmlAttribute("modify_date")]
    public DateTime ModifiedDate { get; set; }
}

[XmlType("o_r")]
[XmlTable("objects")]
[DebuggerDisplay("{Name}")]
public class SysObjectReference
{
    [XmlAttribute("object_id")]
    Int32 ObjectId { get; set; }

    [XmlAttribute("schema_id")]
    Int32 SchemaId { get; set; }

    [XmlAttribute("name")]
    public String Name { get; set; }

    [XmlArray("schema")]
    public SysSchema[] Schemas { get; set; } = Empties<SysSchema>.Array;
}

[XmlType("t")]
[XmlTable("tables")]
[DebuggerDisplay("{Name} ({ObjectId})")]
public class SysTable : SysObject
{
    [XmlArray("columns")]
    public SysColumn[] Columns { get; set; } = Empties<SysColumn>.Array;

    [XmlArray("indexes")]
    public SysIndex[] Indexes { get; set; } = Empties<SysIndex>.Array;

    [XmlArray("foreign_keys")]
    public SysForeignKey[] ForeignKeys { get; set; } = Empties<SysForeignKey>.Array;
}

[XmlType("c")]
[XmlTable("columns")]
[DebuggerDisplay("{Name}")]
public class SysColumn : SysWithComment
{
    [XmlAttribute("object_id")]
    Int32 ObjectId { get; set; }

    [XmlAttribute("column_id")]
    public Int32 ColumnId { get; set; }

    [XmlAttribute("name")]
    public String Name { get; set; }

    [XmlAttribute("is_nullable")]
    public Boolean IsNullable { get; set; }

    [XmlAttribute("user_type_id")]
    public Int32 UserTypeId { get; set; }

    [XmlAttribute("system_type_id")]
    public Int32 SystemTypeId { get; set; }

    [XmlAttribute("max_length")]
    public Int32 MaxLength { get; set; }

    [XmlAttribute("precision")]
    public Int32 Precision { get; set; }

    [XmlAttribute("scale")]
    public Int32 Scale { get; set; }

    [XmlAttribute("is_identity")]
    public Boolean IsIdentity { get; set; }

    [XmlAttribute("is_computed")]
    public Boolean IsComputed { get; set; }

    [XmlArray("systemtype")]
    public SysType[] SystemTypes { get; set; } = Empties<SysType>.Array;

    [XmlArray("usertype")]
    public SysType[] UserTypes { get; set; } = Empties<SysType>.Array;
}

[XmlType("tp")]
[XmlTable("types")]
[DebuggerDisplay("{Name}")]
public class SysType
{
    [XmlAttribute("user_type_id")]
    Int32 UserTypeId { get; set; }

    [XmlAttribute("name")]
    public String Name { get; set; }

    [XmlAttribute("is_assembly_type")]
    public Boolean IsAssemblyType { get; set; }
}

[XmlType("ix_r")]
[XmlTable("indexes")]
[DebuggerDisplay("{Name}")]
public class SysIndexReference
{
    [XmlAttribute("name")]
    public String Name { get; set; }

    [XmlAttribute("object_id")]
    Int32 TableObjectId { get; set; }
}

[XmlType("ix")]
[XmlTable("indexes")]
[DebuggerDisplay("{Name}")]
public class SysIndex : SysWithComment
{
    [XmlAttribute("name")]
    public String Name { get; set; }

    [XmlAttribute("object_id")]
    Int32 TableObjectId { get; set; }

    [XmlAttribute("index_id")]
    Int32 IndexId { get; set; }

    [XmlAttribute("type")]
    public Int32 Type { get; set; }

    [XmlAttribute("type_desc")]
    public String TypeDesc { get; set; }

    [XmlAttribute("is_disabled")]
    public Boolean IsDisabled { get; set; }

    [XmlAttribute("is_unique")]
    public Boolean IsUnique { get; set; }

    [XmlAttribute("is_primary_key")]
    public Boolean IsPrimary { get; set; }

    [XmlAttribute("is_unique_constraint")]
    public Boolean IsUniqueConstraint { get; set; }

    [XmlAttribute("has_filter")]
    public Boolean HasFilter { get; set; }

    [XmlAttribute("is_hypothetical")]
    public Boolean IsHypothetical { get; set; }

    [XmlNoColumn]
    [XmlAttribute("used_pages")]
    public Int32 UsedPages { get; set; }

    [XmlArray("columns")]
    public SysIndexColumn[] Columns { get; set; } = Empties<SysIndexColumn>.Array;

    public (String tag, String reason, String specific)? CheckIntrinsicSupport()
    {
        if (IsDisabled) return ("Disabled", "Disabled indexes can't be used", null);
        if (IsHypothetical) return ("Hypothetical", "Hypothetical indexes can't be searched", null);
        if (HasFilter) return ("Filtered", "Filtered indexes are not yet supported", null);
        if (Type != 1 && Type != 2) return ("Exotic", "Only classical b-tree indexes are supported", $"This index is of type '{TypeDesc}'");
        return null;
    }
}

[XmlType("ix_c")]
[XmlTable("index_columns")]
[DebuggerDisplay("{Name}")]
public class SysIndexColumn
{
    [XmlAttribute("object_id")]
    Int32 TableObjectId { get; set; }

    [XmlAttribute("index_id")]
    Int32 IndexId { get; set; }

    [XmlAttribute("key_ordinal")]
    public Int32 KeyOrdinal { get; set; }

    [XmlAttribute("index_column_id")]
    Int32 IndexColumnId { get; set; }

    [XmlAttribute("column_id")]
    public Int32 ColumnId { get; set; }

    [XmlAttribute("is_descending_key")]
    public Boolean IsDescendingKey { get; set; }

    [XmlAttribute("is_included_column")]
    public Boolean IsIncludedColumn { get; set; }
}

[XmlType("key")]
[XmlTable("key_constraints")]
[DebuggerDisplay("{Name}")]
public class SysKeyConstraint : SysObject
{
    [XmlAttribute("index_id")]
    Int32 UniqueIndexId { get; set; }

    [XmlAttribute("is_system_named")]
    public Boolean IsSystemNamed { get; set; }
}

[XmlType("fk")]
[XmlTable("foreign_keys")]
[DebuggerDisplay("{Name}")]
public class SysForeignKey : SysObject
{
    [XmlAttribute("parent_object_id")]
    Int32 ParentObjectId { get; set; }

    [XmlAttribute("referenced_object_id")]
    Int32 ReferencedObjectId { get; set; }

    [XmlAttribute("key_index_id")]
    Int32 ReferencedIndexlikeId { get; set; }

    [XmlAttribute("is_disabled")]
    public Boolean IsDisabled { get; set; }

    [XmlAttribute("is_not_trusted")]
    public Boolean IsNotTrusted { get; set; }

    [XmlAttribute("is_system_named")]
    public Boolean IsSystemNamed { get; set; }

    [XmlArray("columns")]
    public SysForeignKeyColumn[] Columns { get; set; } = Empties<SysForeignKeyColumn>.Array;

    [XmlArray("referenced_table")]
    public SysObjectReference[] ReferencedTables { get; set; } = Empties<SysObjectReference>.Array;

    [XmlArray("referenced_index")]
    public SysIndexReference[] ReferencedIndexes { get; set; } = Empties<SysIndexReference>.Array;

    public (String tag, String reason, String specific)? CheckIntrinsicSupport()
    {
        if (IsDisabled) return ("Disabled", "The foreign key is disabled", null);
        if (IsNotTrusted) return ("Not trusted", "Untrusted foreign keys are ignored", null);
        return null;
    }
}



[XmlType("fk_c")]
[XmlTable("foreign_key_columns")]
[DebuggerDisplay("{Name}")]
public class SysForeignKeyColumn
{
    [XmlAttribute("constraint_object_id")]
    Int32 ConstraintObjectId { get; set; }

    [XmlAttribute("constraint_column_id")]
    Int32 ConstraintColumnId { get; set; }

    [XmlAttribute("parent_object_id")]
    Int32 ParentObjectId { get; set; }

    [XmlAttribute("parent_column_id")]
    public Int32 ParentColumnId { get; set; }

    [XmlAttribute("referenced_object_id")]
    Int32 ReferencedObjectId { get; set; }

    [XmlAttribute("referenced_column_id")]
    Int32 ReferencedColumnId { get; set; }
}

public static class SystemSchema
{
    public static CsdRoot GetSchema()
    {
        return new CsdRoot
        {
            Tables = new[]
            {
                MakeCsdTable<SysObjectReference>(),
                MakeCsdTable<SysSchema>(),
                MakeCsdTable<SysType>(),

                MakeCsdTable<SysTable>(),
                MakeCsdTable<SysColumn>(),
                MakeCsdTable<SysIndex>(),
                MakeCsdTable<SysIndexColumn>(),
                MakeCsdTable<SysForeignKey>(),
                MakeCsdTable<SysForeignKeyColumn>()
            }
        };
    }

    public static CsdTable MakeCsdTable<T>()
        where T : class
    {
        var md = XmlEntitiyMetata<T>.Instance;

        return MakeCsdTable(md.TableName, md.Columns);
    }


    public static CsdTable MakeCsdTable(String name, CsdColumn[] columns)
    {
        var table = new CsdTable
        {
            Columns = columns
        };

        table.Name = GetTableName(name);

        return table;
    }

    static ObjectName GetTableName(String name)
        => new ObjectName("sys", name);

    public static IEnumerable<Relation> GetRelations()
    {
        var sysSchemaIdNames = new[] { "schema_id" };
        var sysObjectIdNames = new[] { "object_id" };
        var sysObjectIdAndIndexIdNames = new[] { "object_id", "index_id" };

        yield return new Relation
        {
            Principal = new RelationEnd { TableName = GetTableName("schemas"), Name = "tables", ColumnNames = sysSchemaIdNames },
            Dependent = new RelationEnd { TableName = GetTableName("tables"), Name = "schema", ColumnNames = sysSchemaIdNames }
        };

        yield return new Relation
        {
            Principal = new RelationEnd { TableName = GetTableName("tables"), Name = "columns", ColumnNames = sysObjectIdNames },
            Dependent = new RelationEnd { TableName = GetTableName("columns"), Name = "table", ColumnNames = sysObjectIdNames }
        };

        yield return new Relation
        {
            Principal = new RelationEnd { TableName = GetTableName("columns"), Name = "systemtype", ColumnNames = new[] { "system_type_id" } },
            Dependent = new RelationEnd { TableName = GetTableName("types"), Name = "referencing_columns_as_systemtype", ColumnNames = new[] { "user_type_id" } }
        };

        yield return new Relation
        {
            Principal = new RelationEnd { TableName = GetTableName("columns"), Name = "usertype", ColumnNames = new[] { "user_type_id" } },
            Dependent = new RelationEnd { TableName = GetTableName("types"), Name = "referencing_columns_as_usertype", ColumnNames = new[] { "user_type_id" } }
        };

        yield return new Relation
        {
            Principal = new RelationEnd { TableName = GetTableName("tables"), Name = "indexes", ColumnNames = sysObjectIdNames },
            Dependent = new RelationEnd { TableName = GetTableName("indexes"), Name = "table", ColumnNames = sysObjectIdNames }
        };

        yield return new Relation
        {
            Principal = new RelationEnd { TableName = GetTableName("indexes"), Name = "columns", ColumnNames = sysObjectIdAndIndexIdNames },
            Dependent = new RelationEnd { TableName = GetTableName("index_columns"), Name = "index", ColumnNames = sysObjectIdAndIndexIdNames }
        };

        yield return new Relation
        {
            Principal = new RelationEnd { TableName = GetTableName("tables"), Name = "foreign_keys", ColumnNames = new[] { "object_id" } },
            Dependent = new RelationEnd { TableName = GetTableName("foreign_keys"), Name = "table", ColumnNames = new[] { "parent_object_id" } }
        };

        yield return new Relation
        {
            Principal = new RelationEnd { TableName = GetTableName("foreign_keys"), Name = "columns", ColumnNames = new[] { "object_id" } },
            Dependent = new RelationEnd { TableName = GetTableName("foreign_key_columns"), Name = "foreign_key", ColumnNames = new[] { "constraint_object_id" } }
        };

        yield return new Relation
        {
            Principal = new RelationEnd { TableName = GetTableName("foreign_keys"), Name = "referenced_table", ColumnNames = new[] { "referenced_object_id" } },
            Dependent = new RelationEnd { TableName = GetTableName("objects"), Name = "referencing_foreign_keys", ColumnNames = new[] { "object_id" } }
        };

        yield return new Relation
        {
            Principal = new RelationEnd { TableName = GetTableName("foreign_keys"), Name = "referenced_index", ColumnNames = new[] { "referenced_object_id", "key_index_id" } },
            Dependent = new RelationEnd { TableName = GetTableName("indexes"), Name = "referencing_foreign_keys", ColumnNames = new[] { "object_id", "index_id" } }
        };

        yield return new Relation
        {
            Principal = new RelationEnd { TableName = GetTableName("objects"), Name = "schema", ColumnNames = sysSchemaIdNames },
            Dependent = new RelationEnd { TableName = GetTableName("schemas"), Name = "objects", ColumnNames = sysSchemaIdNames }
        };
    }
}
