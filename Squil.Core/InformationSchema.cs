using Squil.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace Squil
{
    [XmlRoot("root")]
    public class ISRoot
    {
        [XmlArray("INFORMATION_SCHEMA_TABLES")]
        public ISTable[] Tables { get; set; }
    }

    public interface IISObjectNamable
    {
        public String Catalog { get; set; }

        public String Schema { get; set; }

        public String Name { get; set; }
    }

    [XmlType("t")]
    [DebuggerDisplay("{Name}")]
    public class ISTable : IISObjectNamable
    {
        static ISColumn[] emptyColumns = new ISColumn[0];

        static ISConstraint[] emptyConstraints = new ISConstraint[0];

        static SysTable[] emptySysTables = new SysTable[0];

        [XmlAttribute("TABLE_CATALOG")]
        public String Catalog { get; set; }

        [XmlAttribute("TABLE_SCHEMA")]
        public String Schema { get; set; }

        [XmlAttribute("TABLE_NAME")]
        public String Name { get; set; }

        [XmlAttribute("TABLE_TYPE")]
        public String Type { get; set; }

        [XmlArray("columns")]
        public ISColumn[] Columns { get; set; } = emptyColumns;

        [XmlArray("constraints")]
        public ISConstraint[] Constraints { get; set; } = emptyConstraints;

        [XmlArray("sys-table")]
        public SysTable[] SysTables { get; set; } = emptySysTables;

    }

    [XmlType("c")]
    [DebuggerDisplay("{COLUMN_NAME}")]
    public class ISColumn
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
    [DebuggerDisplay("{Name}")]
    public class ISConstraint : IISObjectNamable
    {
        [XmlAttribute("CONSTRAINT_CATALOG")]
        public String Catalog { get; set; }

        [XmlAttribute("CONSTRAINT_SCHEMA")]
        public String Schema { get; set; }

        [XmlAttribute("CONSTRAINT_NAME")]
        public String Name { get; set; }

        [XmlAttribute]
        public String CONSTRAINT_TYPE { get; set; }

        [XmlArray("columns")]
        public ISConstraintColumn[] Columns { get; set; }

        [XmlArray("referential")]
        public ISConstraintReferetial[] Referentials { get; set; }
    }

    [XmlType("referential")]
    public class ISConstraintReferetial : IISObjectNamable
    {
        [XmlAttribute("UNIQUE_CONSTRAINT_CATALOG")]
        public String Catalog { get; set; }

        [XmlAttribute("UNIQUE_CONSTRAINT_SCHEMA")]
        public String Schema { get; set; }

        [XmlAttribute("UNIQUE_CONSTRAINT_NAME")]
        public String Name { get; set; }
    }

    [XmlType("cc")]
    [DebuggerDisplay("{COLUMN_NAME}")]
    public class ISConstraintColumn
    {
        [XmlAttribute]
        public String COLUMN_NAME { get; set; }
    }

    #region sys tables

    [XmlType("sys-t")]
    [DebuggerDisplay("{Name} ({ObjectId})")]
    public class SysTable
    {
        [XmlAttribute("object_id")]
        public Int32 ObjectId { get; set; }

        [XmlAttribute("name")]
        public String Name { get; set; }

        [XmlArray("indexes")]
        public SysIndex[] SysIndexes { get; set; } = Empties<SysIndex>.Array;
    }

    [XmlType("i")]
    [DebuggerDisplay("{Name}")]
    public class SysIndex
    {
        [XmlAttribute("object_id")]
        public Int32 ObjectId { get; set; }

        [XmlAttribute("index_id")]
        public Int32 IndexId { get; set; }

        [XmlAttribute("name")]
        public String Name { get; set; }

        [XmlAttribute("type")]
        public Int32 Type { get; set; }

        [XmlAttribute("type_desc")]
        public String TypeDesc { get; set; }


        [XmlAttribute("is_disabled")]
        public Boolean IsDisabled { get; set; }

        [XmlAttribute("is_unique")]
        public Boolean IsUnique { get; set; }

        [XmlAttribute("has_filter")]
        public Boolean HasFilter { get; set; }

        [XmlAttribute("is_hypothetical")]
        public Boolean IsHypothetical { get; set; }

        [XmlArray("columns")]
        public SysIndexColumn[] Columns { get; set; } = Empties<SysIndexColumn>.Array;

        public Boolean IsSupported() => Type >= 1 && Type <= 2 && !IsDisabled && !HasFilter && !IsHypothetical;
    }

    [XmlType("c")]
    [DebuggerDisplay("{Name}")]
    public class SysIndexColumn
    {
        [XmlAttribute("object_id")]
        public Int32 ObjectId { get; set; }

        [XmlAttribute("index_id")]
        public Int32 IndexId { get; set; }

        [XmlAttribute("index_column_id")]
        public Int32 IndexColumnId { get; set; }

        [XmlAttribute("column_id")]
        public Int32 ColumnId { get; set; }
    }

    [XmlType("sys-c")]
    [DebuggerDisplay("{Name}")]
    public class SysColumn
    {
        [XmlAttribute("object_id")]
        public Int32 ObjectId { get; set; }

        [XmlAttribute("column_id")]
        public Int32 ColumnId { get; set; }

        [XmlAttribute("name")]
        public String Name { get; set; }
    }

    #endregion

    public static class InformationSchemaSchema
    {
        public static ISRoot GetSchema()
        {
            return new ISRoot
            {
                Tables = new[]
                {
                    MakeISTable("TABLES", GetKeyColumns("TABLE").Concat(new[] { "TABLE_TYPE" /* complete */ })),
                    MakeISTable("COLUMNS", GetKeyColumns("TABLE").Concat(new [] { "COLUMN_NAME", "DATA_TYPE", "ORDINAL_POSITION" })),
                    MakeISTable("TABLE_CONSTRAINTS",
                        GetKeyColumns("CONSTRAINT"),
                        GetKeyColumns("TABLE"),
                        new [] { "CONSTRAINT_TYPE" }),
                    MakeISTable("REFERENTIAL_CONSTRAINTS",
                        GetKeyColumns("CONSTRAINT"),
                        GetKeyColumns("UNIQUE_CONSTRAINT"),
                        new [] { "MATCH_OPTION", "UPDATE_RULE", "DELETE_RULE" }),
                    MakeISTable("KEY_COLUMN_USAGE",
                        GetKeyColumns("TABLE"),
                        GetKeyColumns("CONSTRAINT"),
                        new [] { "COLUMN_NAME", "ORDINAL_POSITION" }),

                    MakeISTable("sys", "tables",
                        new [] { "object_id", "name" }),
                    MakeISTable("sys", "columns",
                        new [] { "object_id", "column_id", "name" }),
                    MakeISTable("sys", "indexes",
                        new [] { "object_id", "index_id", "name", "type", "type_desc", "is_disabled", "is_unique", "has_filter", "is_hypothetical" }),
                    MakeISTable("sys", "index_columns",
                        new [] { "object_id", "index_id", "index_column_id", "column_id" })
                }
            };
        }

        public static ISTable MakeISTable(String name, params IEnumerable<String>[] columns)
            => MakeISTable(null, name, columns);

        public static ISTable MakeISTable(String name, IEnumerable<String> columns)
            => MakeISTable(null, name, columns);

        public static ISTable MakeISTable(String schema, String name, params IEnumerable<String>[] columns)
        {
            return MakeISTable(schema, name, columns.SelectMany(cls => cls).ToArray());
        }

        public static ISTable MakeISTable(String schema, String name, IEnumerable<String> columns)
        {
            var table = new ISTable
            {
                Columns = columns.Select(n => new ISColumn { COLUMN_NAME = n, DATA_TYPE = "varchar" }).ToArray()
            };

            table.SetName(GetISTableName(name, schema));

            return table;
        }

        public static IEnumerable<Relation> GetRelations()
        {
            var tableKeyColumnNames = GetKeyColumns("TABLE").ToArray();
            var constraintKeyColumnNames = GetKeyColumns("CONSTRAINT").ToArray();
            var sysObjectIdNames = new[] { "object_id" };
            var sysObjectIdAndIndexIdNames = new[] { "object_id", "index_id" };

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


            yield return new Relation
            {
                Principal = new RelationEnd { TableName = GetISTableName("tables", "sys"), Name = "columns", ColumnNames = sysObjectIdNames },
                Dependent = new RelationEnd { TableName = GetISTableName("columns", "sys"), Name = "table", ColumnNames = sysObjectIdNames }
            };

            yield return new Relation
            {
                Principal = new RelationEnd { TableName = GetISTableName("tables", "sys"), Name = "indexes", ColumnNames = sysObjectIdNames },
                Dependent = new RelationEnd { TableName = GetISTableName("indexes", "sys"), Name = "table", ColumnNames = sysObjectIdNames }
            };

            yield return new Relation
            {
                Principal = new RelationEnd { TableName = GetISTableName("indexes", "sys"), Name = "columns", ColumnNames = sysObjectIdAndIndexIdNames },
                Dependent = new RelationEnd { TableName = GetISTableName("index_columns", "sys"), Name = "index", ColumnNames = sysObjectIdAndIndexIdNames }
            };
        }

        private static IEnumerable<String> GetKeyColumns(String type) => ISBaseKeyNames.Select(n => $"{type}_{n}");

        static ObjectName GetISTableName(String name, String schema = null)
            => new ObjectName(schema ?? "INFORMATION_SCHEMA", name);

        static String[] ISBaseKeyNames = new[] { "CATALOG", "SCHEMA", "NAME" };
    }
}
