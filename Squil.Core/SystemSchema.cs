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
    public class SysRoot
    {
        [XmlArray("sys_schemas")]
        public SysSchema[] Schemas { get; set; }
    }

    [XmlType("s")]
    [DebuggerDisplay("{Name}")]
    public class SysSchema
    {
        [XmlAttribute("name")]
        public String Name { get; set; }

        [XmlAttribute("schema_id")]
        public Int32 SchemaId { get; set; }

        [XmlArray("tables")]
        public SysTable[] Tables { get; set; } = Empties<SysTable>.Array;
    }

    [XmlType("t")]
    [DebuggerDisplay("{Name} ({ObjectId})")]
    public class SysTable
    {
        [XmlAttribute("object_id")]
        public Int32 ObjectId { get; set; }

        [XmlAttribute("schema_id")]
        public Int32 SchemaId { get; set; }

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

    [XmlType("ic")]
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

    [XmlType("c")]
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

    public static class SystemSchema
    {
        public static ISRoot GetSchema()
        {
            return new ISRoot
            {
                Tables = new[]
                {
                    MakeISTable("schemas",
                        new [] { "name", "schema_id" }),
                    MakeISTable("tables",
                        new [] { "object_id", "schema_id", "name" }),
                    MakeISTable("columns",
                        new [] { "object_id", "column_id", "name" }),
                    MakeISTable("indexes",
                        new [] { "object_id", "index_id", "name", "type", "type_desc", "is_disabled", "is_unique", "has_filter", "is_hypothetical" }),
                    MakeISTable("index_columns",
                        new [] { "object_id", "index_id", "index_column_id", "column_id" })
                }
            };
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

            table.SetName(GetTableName(name));

            return table;
        }

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
                Principal = new RelationEnd { TableName = GetTableName("tables"), Name = "indexes", ColumnNames = sysObjectIdNames },
                Dependent = new RelationEnd { TableName = GetTableName("indexes"), Name = "table", ColumnNames = sysObjectIdNames }
            };

            yield return new Relation
            {
                Principal = new RelationEnd { TableName = GetTableName("indexes"), Name = "columns", ColumnNames = sysObjectIdAndIndexIdNames },
                Dependent = new RelationEnd { TableName = GetTableName("index_columns"), Name = "index", ColumnNames = sysObjectIdAndIndexIdNames }
            };
        }

        static ObjectName GetTableName(String name)
            => new ObjectName("sys", name);

        static String[] ISBaseKeyNames = new[] { "CATALOG", "SCHEMA", "NAME" };
    }
}
