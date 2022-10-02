using System;

namespace Squil.SchemaBuilding;

public record CsdUnsupportedReason(String Tag, String Reason, String Specific)
{
    public static implicit operator CsdUnsupportedReason((String tag, String reason, String specific) tuple)
    {
        return new CsdUnsupportedReason(tuple.tag, tuple.reason, tuple.specific);
    }
}

public class CsdBase
{
    public String Comment { get; set; }

    public List<String> Comments { get; set; }

    public CsdUnsupportedReason UnsupportedReason { get; set; }

    public Boolean IsSupported => UnsupportedReason == null;
    public Boolean IsUnsupported => UnsupportedReason != null;
}

public class CsdRoot : CsdBase
{
    public CsdTable[] Tables { get; set; }

    public DateTime TimeStamp { get; set; }
}

public class CsdTable : CsdBase
{
    public ObjectName Name { get; set; }

    public Boolean IsHeap { get; set; }

    public CsdTableType Type { get; set; }

    public CsdColumn[] Columns { get; set; }

    public CsdKeyish[] Keyishs { get; set; }

    public Int32? UsedKb { get; set; }
}

public class CsdColumn : CsdBase
{
    public String Name { get; set; }

    public String DataTypeAlias { get; set; }

    public String DataType { get; set; }

    public Boolean IsSystemType { get; set; }

    public Boolean IsAssemblyType { get; set; }

    public Boolean IsNullable { get; set; }

    public Int32 ColumnId { get; set; }
}

public enum CsdKeyishType
{
    Unknown,
    Pk,
    Ak,
    Fk,
    Ix
}

public enum CsdTableType
{
    Unknown,
    Table,
    View
}

public class CsdKeyish : CsdBase
{
    public ObjectName Name { get; set; }

    public CsdKeyishType Type { get; set; }

    public DirectedColumnName[] Columns { get; set; }
}

public class CsdIndexlike : CsdKeyish
{
    public Boolean IsUnique { get; set; }

    public Int32? UsedKb { get; set; }
}

public class CsdForeignKey : CsdKeyish
{
    public ObjectName ReferencedIndexlike { get; set; }
}

public static class CsdExtensions
{
    public static CsdKeyishType GetCsdType(this SysIndex index)
    {
        if (index.IsPrimary)
        {
            return CsdKeyishType.Pk;
        }
        else if (index.IsUniqueConstraint)
        {
            return CsdKeyishType.Ak;
        }
        else
        {
            return CsdKeyishType.Ix;
        }
    }

    public static CsdRoot CreateCsd(this SysRoot root)
    {
        var csdTables = new List<CsdTable>();

        foreach (var schema in root.Schemas)
        {
            foreach (var table in schema.Tables)
            {
                Int32? tableSizeKb = null;

                var tableName = new ObjectName(schema.Name, table.Name);

                var isHeap = false;

                var csdColumns = (
                    from c in table.Columns
                    // Likely a bug in SQL Server, a mere dbdatareader can't read custom user types as seen in the AdventureWorks 2019 LT on SalesLT.Address.StateProvince
                    // Let's hope we always have a system type in such cases
                    let usertype = c.UserTypes.SingleOrDefault($"Unexpectedly no unique usertype at column {schema.Name}.{table.Name}.{c.Name}")
                    let systemtype = c.SystemTypes.SingleOrDefault($"Unexpectedly no unique systemtype at column {schema.Name}.{table.Name}.{c.Name}")
                    select new CsdColumn
                    {
                        Name = c.Name,
                        DataTypeAlias = usertype?.Name,
                        DataType = (systemtype ?? usertype)?.Name ?? throw new Exception($"Neither user type nor system type found"),
                        IsSystemType = systemtype != null,
                        IsAssemblyType = usertype?.IsAssemblyType ?? false,
                        IsNullable = c.IsNullable,
                        ColumnId = c.ColumnId,
                        Comment = c.Comment
                    }
                ).ToArray();

                var csdKeyishs = new List<CsdKeyish>();

                foreach (var index in table.Indexes)
                {
                    if (index.Type <= 1)
                    {
                        tableSizeKb = index.UsedPages * 8;
                    }

                    if (index.Type == 0)
                    {
                        isHeap = true;

                        // Heaps appear as indexes, but that makes no sense for Squil to deal with.
                        continue;
                    }

                    var directedColumnNames = (
                        from ic in index.Columns
                        where !ic.IsIncludedColumn
                        let c = table.Columns.Single(c2 => c2.ColumnId == ic.ColumnId)
                        let d = ic.IsDescendingKey ? IndexDirection.Desc : IndexDirection.Asc
                        select new DirectedColumnName(c.Name, d)
                    ).ToArray();

                    var isKey = index.IsPrimary || index.IsUniqueConstraint;

                    var name = new ObjectName(schema.Name, table.Name, index.Name);

                    var intrinsicallyUnsupported = index.CheckIntrinsicSupport()?.Apply(t => new CsdUnsupportedReason(t.tag, t.reason, t.specific));

                    csdKeyishs.Add(new CsdIndexlike
                    {
                        Name = name,
                        Columns = directedColumnNames,
                        IsUnique = index.IsUnique,
                        Type = index.GetCsdType(),
                        UsedKb = index.UsedPages * 8,
                        UnsupportedReason = intrinsicallyUnsupported
                    });
                }

                foreach (var fk in table.ForeignKeys)
                {
                    var directedColumnNames = (
                        from fkc in fk.Columns
                        let c = table.Columns.Single(c2 => c2.ColumnId == fkc.ParentColumnId)
                        select new DirectedColumnName(c.Name)
                    ).ToArray();

                    var name = new ObjectName(schema.Name, fk.Name);

                    var referencedTable = fk.ReferencedTables.Single("Unexpectedly not unambiguously having a referenced table");

                    var referencedSchema = referencedTable.Schemas.Single("Unexpectedly not unambiguously having a referenced table schema");

                    var referencedIndex = fk.ReferencedIndexes.Single("Unexpectedly not unambiguously having a referenced index");

                    var referencedIndexlike = new ObjectName(referencedSchema.Name, referencedTable.Name, referencedIndex.Name);

                    var intrinsicallyUnsupported = fk.CheckIntrinsicSupport()?.Apply(t => new CsdUnsupportedReason(t.tag, t.reason, t.specific));

                    csdKeyishs.Add(new CsdForeignKey
                    {
                        Name = name,
                        Columns = directedColumnNames,
                        Type = CsdKeyishType.Fk,
                        ReferencedIndexlike = referencedIndexlike,
                        UnsupportedReason = intrinsicallyUnsupported
                    });
                }

                csdTables.Add(new CsdTable
                {
                    Name = tableName,
                    Columns = csdColumns,
                    Keyishs = csdKeyishs.ToArray(),
                    Type = CsdTableType.Table,
                    UsedKb = tableSizeKb,
                    Comment = table.Comment,
                    IsHeap = isHeap
                });
            }
        }

        return new CsdRoot { Tables = csdTables.ToArray(), Comment = root.Comment, TimeStamp = root.SchemaDate };
    }

    public static CsdRoot CreateCsd(this ISRoot root)
    {
        var csdTables = new List<CsdTable>();

        foreach (var table in root.Tables)
        {
            var tableName = new ObjectName(table.TableCatalog, table.TableSchema, table.TableName);

            var csdColumns = (
                from c in table.Columns
                select new CsdColumn
                {
                    Name = c.COLUMN_NAME,
                    DataType = c.DATA_TYPE,
                    IsNullable = c.IS_NULLABLE == "YES"
                }
            ).ToArray();

            var csdKeyishs = new List<CsdKeyish>();

            foreach (var c in table.Constraints)
            {
                var directedColumnNames = c.Columns.Select(c2 => new DirectedColumnName(c2.COLUMN_NAME)).ToArray();

                var name = new ObjectName(c.ConstraintCatalog, c.ConstraintSchema, c.ConstraintName);

                switch (c.ConstraintType)
                {
                    case "PRIMARY KEY":
                    case "UNIQUE KEY":
                        csdKeyishs.Add(new CsdIndexlike
                        {
                            Name = name,
                            Columns = directedColumnNames,
                            IsUnique = true,
                            Type = c.ConstraintType == "PRIMARY KEY" ? CsdKeyishType.Pk : CsdKeyishType.Ak
                        });
                        break;

                    case "FOREIGN KEY":
                        var referential = c.Referentials.Single("Unexpectedly no unique referential entry in foreign key constraint");

                        csdKeyishs.Add(new CsdForeignKey
                        {
                            Name = name,
                            Type = CsdKeyishType.Fk,
                            Columns = directedColumnNames,
                            ReferencedIndexlike = referential.GetReferentialContraintName()
                        });

                        break;

                    default:
                        break;
                }
            }

            csdTables.Add(new CsdTable
            {
                Name = table.GetTableName(),
                Columns = csdColumns.ToArray(),
                Keyishs = csdKeyishs.ToArray(),
                Type = table.Type == "VIEW" ? CsdTableType.View : CsdTableType.Table
            });
        }

        return new CsdRoot
        {
            Tables = csdTables.ToArray(),
            TimeStamp = root.SchemaDate
        };
    }
}

