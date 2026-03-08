using TaskTracker.Application.DTOs;
using TaskTracker.Application.Services;
using TaskTracker.Application.Tests.Support;

namespace TaskTracker.Application.Tests;

public sealed class TaskServiceDeleteAndQueryTests
{
    /// <summary>
    /// Verifica que una tarea eliminada deje de estar disponible para consulta.
    /// </summary>
    /// <returns>Una tarea completada cuando la validación de eliminación finaliza.</returns>
    [Fact]
    public async Task DeleteAsync_ShouldRemoveTask()
    {
        var repository = new InMemoryTaskRepository();
        var service = new TaskService(repository);

        var id = await service.CreateAsync(new CreateTaskRequest
        {
            Title = "Eliminar luego de prueba"
        });

        var deleted = await service.DeleteAsync(id);
        var loaded = await service.GetByIdAsync(id);

        Assert.True(deleted);
        Assert.Null(loaded);
    }

    /// <summary>
    /// Verifica que la consulta general devuelva las tareas creadas.
    /// </summary>
    /// <returns>Una tarea completada cuando la validación de consulta finaliza.</returns>
    [Fact]
    public async Task GetAllAsync_ShouldReturnCreatedTasks()
    {
        var service = new TaskService(new InMemoryTaskRepository());

        await service.CreateAsync(new CreateTaskRequest { Title = "Tarea A" });
        await service.CreateAsync(new CreateTaskRequest { Title = "Tarea B" });

        var tasks = await service.GetAllAsync();

        Assert.Equal(2, tasks.Count);
    }
}
