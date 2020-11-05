﻿using Acidui.Core;
using System;
using System.Data.SqlClient;

namespace Acidui
{
    public class AciduiContext
    {
        private readonly string connectionString;

        public CMRoot CircularModel { get; set; }

        public QueryGenerator QueryGenerator { get; set; }

        public AciduiContext(String connectionString = "server=.\\;database=blipboard;integrated security=true")
        {
            this.connectionString = connectionString;

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
