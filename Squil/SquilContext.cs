using System.Data.SqlClient;

namespace Squil
{
    public class SquilContext
    {
        private readonly string connectionString;

        public CMRoot CircularModel { get; set; }

        public QueryGenerator QueryGenerator { get; set; }

        public SquilContext(String connectionString)
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
