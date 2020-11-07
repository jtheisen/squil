using Acidui.Core;
using Microsoft.Extensions.Configuration;
using System;
using System.Data.SqlClient;

namespace Acidui
{
    public class AciduiContext
    {
        private readonly string connectionString;

        public CMRoot CircularModel { get; set; }

        public QueryGenerator QueryGenerator { get; set; }

        public AciduiContext(IConfiguration configuration)
        {
            this.connectionString = configuration["DatabaseConnection"];

            using var connection = GetConnection();

            CircularModel = connection.GetCircularModel();

            QueryGenerator = new QueryGenerator(CircularModel);
        }

        public SqlConnection GetConnection()
        {
            var connection = new SqlConnection(connectionString);

            connection.Open();

            return connection;
        }
    }
}
