using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace TaskTracker.Infrastructure.Persistence;

public interface ISqlConnectionFactory
{
    /// <summary>
    /// Crea una nueva conexión de base de datos lista para ser utilizada por la infraestructura.
    /// </summary>
    /// <returns>Una conexión ADO.NET configurada para SQL Server.</returns>
    IDbConnection CreateConnection();
}

public sealed class SqlConnectionFactory(IConfiguration configuration) : ISqlConnectionFactory
{
    /// <summary>
    /// Construye una conexión SQL a partir de la cadena de conexión configurada.
    /// </summary>
    /// <returns>Una conexión a SQL Server inicializada con la configuración vigente.</returns>
    public IDbConnection CreateConnection()
    {
        var connectionString = configuration.GetConnectionString("TaskTrackerDb")
            ?? throw new InvalidOperationException("Connection string 'TaskTrackerDb' is not configured.");

        return new SqlConnection(connectionString);
    }
}
