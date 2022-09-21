using System.Xml.Serialization;

namespace Squil.SchemaBuilding;

[XmlRoot("root")]
public class ISRoot
{
    [XmlArray("INFORMATION_SCHEMA.TABLES")]
    public ISTable[] Tables { get; set; }
}

public class ISWithTable
{
    [XmlAttribute("TABLE_CATALOG")]
    public String TableCatalog { get; set; }

    [XmlAttribute("TABLE_SCHEMA")]
    public String TableSchema { get; set; }

    [XmlAttribute("TABLE_NAME")]
    public String TableName { get; set; }

    public void SetTableName(ObjectName name) => (TableCatalog, TableSchema, TableName) = name;
    public ObjectName GetTableName() => new ObjectName(TableCatalog, TableSchema, TableName);
}

public class ISWithConstraint : ISWithTable
{
    [XmlAttribute("CONSTRAINT_CATALOG")]
    public String ConstraintCatalog { get; set; }

    [XmlAttribute("CONSTRAINT_SCHEMA")]
    public String ConstraintSchema { get; set; }

    [XmlAttribute("CONSTRAINT_NAME")]
    public String ConstraintName { get; set; }

    public void SetConstraintName(ObjectName name) => (ConstraintCatalog, ConstraintSchema, ConstraintName) = name;
    public ObjectName GetConstraintName() => new ObjectName(ConstraintCatalog, ConstraintSchema, ConstraintName);
}

[XmlType("t")]
[XmlTable("TABLES")]
[DebuggerDisplay("{Name}")]
public class ISTable : ISWithTable
{
    static ISColumn[] emptyColumns = new ISColumn[0];

    static ISConstraint[] emptyConstraints = new ISConstraint[0];

    [XmlAttribute("TABLE_TYPE")]
    public String Type { get; set; }

    [XmlArray("columns")]
    public ISColumn[] Columns { get; set; } = emptyColumns;

    [XmlArray("constraints")]
    public ISConstraint[] Constraints { get; set; } = emptyConstraints;
}

[XmlType("c")]
[XmlTable("COLUMNS")]
[DebuggerDisplay("{COLUMN_NAME}")]
public class ISColumn : ISWithTable
{
    [XmlAttribute]
    public String COLUMN_NAME { get; set; }

    [XmlAttribute]
    public Int32 ORDINAL_POSITION { get; set; }

    [XmlAttribute]
    public String IS_NULLABLE { get; set; }

    [XmlAttribute]
    public String DATA_TYPE { get; set; }
}

[XmlType("cnstrnt")]
[XmlTable("TABLE_CONSTRAINTS")]
[DebuggerDisplay("{Name}")]
public class ISConstraint : ISWithConstraint
{
    [XmlAttribute("CONSTRAINT_TYPE")]
    public String ConstraintType { get; set; }

    [XmlArray("columns")]
    public ISConstraintColumn[] Columns { get; set; }

    [XmlArray("referential")]
    public ISConstraintReferetial[] Referentials { get; set; }
}

[XmlType("referential")]
[XmlTable("REFERENTIAL_CONSTRAINTS")]
public class ISConstraintReferetial : ISWithConstraint
{
    [XmlAttribute("UNIQUE_CONSTRAINT_CATALOG")]
    public String UniqueConstraintCatalog { get; set; }

    [XmlAttribute("UNIQUE_CONSTRAINT_SCHEMA")]
    public String UniqueConstraintSchema { get; set; }

    [XmlAttribute("UNIQUE_CONSTRAINT_NAME")]
    public String UniqueConstraintName { get; set; }

    public void SetReferentialContraintName(ObjectName name) => (UniqueConstraintCatalog, UniqueConstraintSchema, UniqueConstraintName) = name;
    public ObjectName GetReferentialContraintName() => new ObjectName(UniqueConstraintCatalog, UniqueConstraintSchema, UniqueConstraintName);
}

[XmlType("cc")]
[XmlTable("KEY_COLUMN_USAGE")]
[DebuggerDisplay("{COLUMN_NAME}")]
public class ISConstraintColumn : ISWithConstraint
{
    [XmlAttribute]
    public String COLUMN_NAME { get; set; }
}

public static class InformationSchemaSchema
{
    public static ISRoot GetSchema()
    {
        return new ISRoot
        {
            Tables = new[]
            {
                MakeISTable<ISTable>(),
                MakeISTable<ISColumn>(),
                MakeISTable<ISConstraint>(),
                MakeISTable<ISConstraintReferetial>(),
                MakeISTable<ISConstraintColumn>()
            }
        };
    }

    public static ISTable MakeISTable<T>()
        where T : class
    {
        var md = XmlEntitiyMetata<T>.Instance;

        return MakeISTable(md.TableName, md.ColumnNames);
    }

    public static ISTable MakeISTable(String name, params IEnumerable<String>[] columns)
    {
        return MakeISTable(name, columns.SelectMany(cls => cls).ToArray());
    }

    public static ISTable MakeISTable(String name, IEnumerable<String> columns)
    {
        var table = new ISTable
        {
            Columns = columns.Select(n => new ISColumn { COLUMN_NAME = n, DATA_TYPE = "varchar" }).ToArray()
        };

        table.SetTableName(GetISTableName(name));

        return table;
    }

    public static IEnumerable<Relation> GetRelations()
    {
        var tableKeyColumnNames = GetKeyColumns("TABLE").ToArray();
        var constraintKeyColumnNames = GetKeyColumns("CONSTRAINT").ToArray();

        yield return new Relation
        {
            Principal = new RelationEnd { TableName = GetISTableName("TABLES"), Name = "columns", ColumnNames = tableKeyColumnNames },
            Dependent = new RelationEnd { TableName = GetISTableName("COLUMNS"), Name = "table", ColumnNames = tableKeyColumnNames }
        };

        yield return new Relation
        {
            Principal = new RelationEnd { TableName = GetISTableName("TABLES"), Name = "constraints", ColumnNames = tableKeyColumnNames },
            Dependent = new RelationEnd { TableName = GetISTableName("TABLE_CONSTRAINTS"), Name = "table", ColumnNames = tableKeyColumnNames }
        };

        yield return new Relation
        {
            Principal = new RelationEnd { TableName = GetISTableName("TABLE_CONSTRAINTS"), Name = "columns", ColumnNames = constraintKeyColumnNames },
            Dependent = new RelationEnd { TableName = GetISTableName("KEY_COLUMN_USAGE"), Name = "constraint", ColumnNames = constraintKeyColumnNames }
        };

        yield return new Relation
        {
            Principal = new RelationEnd { TableName = GetISTableName("TABLE_CONSTRAINTS"), Name = "referential", ColumnNames = constraintKeyColumnNames },
            Dependent = new RelationEnd { TableName = GetISTableName("REFERENTIAL_CONSTRAINTS"), Name = "constraint", ColumnNames = constraintKeyColumnNames }
        };
    }

    private static IEnumerable<String> GetKeyColumns(String type) => ISBaseKeyNames.Select(n => $"{type}_{n}");

    static ObjectName GetISTableName(String name)
        => new ObjectName("INFORMATION_SCHEMA", name);

    static String[] ISBaseKeyNames = new[] { "CATALOG", "SCHEMA", "NAME" };
}
