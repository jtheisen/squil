using Microsoft.Data.SqlClient;

namespace Squil;

public class ConnectionProbe
{
    public String Name { get; set; }

    public Boolean? CanConnect { get; set; }
}

public class StallDetective
{
    public List<ConnectionProbe> Probes { get; }

    public StallDetective(String connectionString)
    {
        new SqlConnectionStringBuilder(connectionString);
    }
}
