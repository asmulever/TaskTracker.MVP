using Microsoft.Extensions.DependencyInjection;
using TaskTracker.Application.Abstractions;
using TaskTracker.Infrastructure.Persistence;

namespace TaskTracker.Infrastructure.DependencyInjection;

public static class DependencyInjection
{
    /// <summary>
    /// Registra en el contenedor los servicios de infraestructura requeridos por la aplicación.
    /// </summary>
    /// <param name="services">Colección de servicios que recibirá los registros.</param>
    /// <returns>La misma colección de servicios para permitir encadenamiento.</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<ITaskRepository, TaskRepository>();

        return services;
    }
}
