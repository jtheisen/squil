using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Squil.SchemaBuilding;

public record CsdUnsupportedReason(String Tag, String Reason)
{
    public static implicit operator CsdUnsupportedReason((String tag, String reason) tuple)
    {
        return new CsdUnsupportedReason(tuple.tag, tuple.reason);
    }
}

public class CsdBase
{
    public List<String> Comments { get; set; }

    public CsdUnsupportedReason UnsupportedReason { get; set; }

    public Boolean IsSupported => UnsupportedReason == null;
    public Boolean IsUnsupported => UnsupportedReason != null;
}

public class CsdRoot : CsdBase
{
    public CsdTable[] Tables { get; set; }
}

public class CsdTable : CsdBase
{
    public ObjectName Name { get; set; }

    public CsdTableType Type { get; set; }

    public CsdColumn[] Columns { get; set; }

    public CsdKeyish[] Keyishs { get; set; }
}

public class CsdColumn : CsdBase
{
    public String Name { get; set; }

    public String DataType { get; set; }

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
}

public class CsdForeignKey : CsdKeyish
{
    public ObjectName ReferencedIndexlike { get; set; }
}

public static class X
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

    public static CsdRoot Create(SysRoot root)
    {
        var csdTables = new List<CsdTable>();

        foreach (var schema in root.Schemas)
        {
            foreach (var table in schema.Tables)
            {
                var tableName = new ObjectName(schema.Name, table.Name);

                var csdColumns = (
                    from c in table.Columns
                    let type = c.Types.Single("Unexpectedly no unique type at column")
                    select new CsdColumn
                    {
                        Name = c.Name,
                        DataType = type.Name,
                        IsNullable = c.IsNullable,
                        ColumnId = c.ColumnId
                    }
                ).ToArray();

                var csdKeyishs = new List<CsdKeyish>();

                foreach (var index in table.Indexes)
                {
                    var directedColumnNames = (
                        from ic in index.Columns
                        let c = table.Columns.Single(c2 => c2.ColumnId == ic.ColumnId)
                        let d = ic.IsDescendingKey ? IndexDirection.Desc : IndexDirection.Asc
                        select new DirectedColumnName(c.Name, d)
                    ).ToArray();

                    var isKey = index.IsPrimary || index.IsUniqueConstraint;

                    var name = isKey ? new ObjectName(schema.Name, index.Name) : new ObjectName(schema.Name, table.Name, index.Name);

                    var intrinsicallyUnsupported = index.CheckIntrinsicSupport()?.Apply(t => new CsdUnsupportedReason(t.tag, t.reason));

                    csdKeyishs.Add(new CsdIndexlike
                    {
                        Name = name,
                        Columns = directedColumnNames,
                        IsUnique = index.IsUnique,
                        Type = index.GetCsdType(),
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

                    var referencedTable = fk.ReferencedTable;

                    var referencedIndexlike = new ObjectName(referencedTable.Schema.Name, referencedTable.Name, fk.ReferencedIndex.Name);

                    csdKeyishs.Add(new CsdForeignKey
                    {
                        Name = name,
                        Columns = directedColumnNames,
                        Type = CsdKeyishType.Fk,
                        ReferencedIndexlike = referencedIndexlike
                    });
                }

                csdTables.Add(new CsdTable
                {
                    Name = tableName,
                    Columns = csdColumns,
                    Keyishs = csdKeyishs.ToArray(),
                    Type = CsdTableType.Table
                });
            }
        }

        return new CsdRoot { Tables = csdTables.ToArray() };
    }

    public static CsdRoot Create(ISRoot root)
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

                switch (c.ConstraintType)
                {
                    case "PRIMARY KEY":
                    case "UNIQUE KEY":
                        csdKeyishs.Add(new CsdIndexlike
                        {
                            Columns = directedColumnNames,
                            IsUnique = true,
                            Type = c.ConstraintType == "PRIMARY KEY" ? CsdKeyishType.Pk : CsdKeyishType.Ak
                        });
                        break;

                    case "FOREIGN KEY":
                        var referential = c.Referentials.Single("Unexpectedly no unique referential entry in foreign key constraint");

                        csdKeyishs.Add(new CsdForeignKey
                        {
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
            Tables = csdTables.ToArray()
        };
    }
}

