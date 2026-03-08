using TaskTracker.Application.Services;
using TaskTracker.Application.Tests.Support;

namespace TaskTracker.Application.Tests;

public sealed class TaskServiceSmokeTests
{
    /// <summary>
    /// Verifica que el servicio devuelva una colección vacía cuando no existen tareas.
    /// </summary>
    /// <returns>Una tarea completada cuando la validación smoke finaliza.</returns>
    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyCollection_WhenNoData()
    {
        var service = new TaskService(new InMemoryTaskRepository());

        var tasks = await service.GetAllAsync();

        Assert.Empty(tasks);
    }
}
