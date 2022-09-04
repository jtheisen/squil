using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace Squil.Core
{
    public static class SchemaBuildingExtensions
    {
        static ISRoot GetISSchema(this SqlConnection connection)
        {
            using var _ = GetCurrentLedger().GroupingScope(nameof(GetISSchema));

            var cmRootForInfSch = new CMRoot("INFORMATION_SCHEMA");
            cmRootForInfSch.Populate(InformationSchemaSchema.GetSchema());
            cmRootForInfSch.PopulateRoot();
            cmRootForInfSch.Populate(InformationSchemaSchema.GetRelations().ToArray());

            var infSchGenerator = new QueryGenerator(cmRootForInfSch, false);

            var cSchema = infSchGenerator.Query<ISRoot>(connection, new Extent
            {
                RelationName = "",
                Children = new[]
                {
                    new Extent
                    {
                        RelationName = "INFORMATION_SCHEMA.TABLES",
                        Alias = "t",
                        Children = new[]
                        {
                            new Extent
                            {
                                Order = new DirectedColumnName[] { "ORDINAL_POSITION" },
                                RelationName = "columns",
                                Alias = "c"
                            },

                            new Extent
                            {
                                RelationName = "constraints",
                                Alias = "cnstrnt",
                                Children = new[]
                                {
                                    new Extent
                                    {
                                        Order = new DirectedColumnName[] { "ORDINAL_POSITION" },
                                        RelationName = "columns",
                                        Alias = "cc"
                                    },
                                    new Extent
                                    {
                                        RelationName = "referential",
                                        Alias = "referential"
                                    }
                                }
                            }
                        }
                    }
                }
            });

            return cSchema;
        }

        static SysRoot GetSysSchema(this SqlConnection connection)
        {
            using var _ = GetCurrentLedger().GroupingScope(nameof(GetSysSchema));

            var cmRootForSys = new CMRoot("sys");
            cmRootForSys.Populate(SystemSchema.GetSchema());
            cmRootForSys.PopulateRoot();
            cmRootForSys.Populate(SystemSchema.GetRelations().ToArray());

            var sysGenerator = new QueryGenerator(cmRootForSys, false);

            var cSchema = sysGenerator.Query<SysRoot>(connection, new Extent
            {
                RelationName = "",
                Children = new[]
                {
                    new Extent
                    {
                        RelationName = "sys.schemas",
                        Alias = "s",
                        Children = new[]
                        {
                                new Extent
                                {
                                    RelationName = "tables",
                                    Alias = "t",
                                    Children = new[]
                                    {
                                        new Extent
                                        {
                                            Order = new DirectedColumnName[] { "index_id" },
                                            RelationName = "indexes",
                                            Alias = "i",
                                            Children = new[]
                                            {
                                                new Extent
                                                {
                                                    Order = new DirectedColumnName[] { "index_column_id" },
                                                    RelationName = "columns",
                                                    Alias = "ic"
                                                }
                                            }
                                        },
                                        new Extent
                                        {
                                            Order = new DirectedColumnName[] { "column_id" },
                                            RelationName = "columns",
                                            Alias = "c"
                                        }
                                    }
                                }
                        }
                    }
                }
            });

            return cSchema;
        }

        public static CMRoot GetCircularModel(this SqlConnection connection)
        {
            using var _ = GetCurrentLedger().GroupingScope(nameof(GetCircularModel));

            var isSchema = connection.GetISSchema();
            var sysSchema = connection.GetSysSchema();

            // populate from sysschema

            var cmRootForCs = new CMRoot("business model");
            cmRootForCs.Populate(isSchema, sysSchema);
            cmRootForCs.PopulateRoot();
            cmRootForCs.PopulateRelationsFromForeignKeys();
            cmRootForCs.Closeup();

            return cmRootForCs;
        }
    }
}
