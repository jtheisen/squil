using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace Acidui.Core
{
    public static class SchemaBuildingExtensions
    {
        public static CMRoot GetCircularModel(this SqlConnection connection)
        {
            var cmRootForInfSch = new CMRoot("INFORMATION_SCHEMA");
            cmRootForInfSch.Populate(InformationSchemaSchema.GetSchema());
            cmRootForInfSch.PopulateRoot();
            cmRootForInfSch.Populate(InformationSchemaSchema.GetRelations().ToArray());

            var infSchGenerator = new QueryGenerator(cmRootForInfSch, true);

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
                                Order = new[] { "ORDINAL_POSITION" },
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
                                        Order = new[] { "ORDINAL_POSITION" },
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

            var cmRootForCs = new CMRoot("business model");
            cmRootForCs.Populate(cSchema);
            cmRootForCs.PopulateRoot();
            cmRootForCs.PopulateRelationsFromForeignKeys();

            return cmRootForCs;
        }
    }
}
