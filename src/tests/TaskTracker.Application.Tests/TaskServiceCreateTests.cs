using TaskTracker.Application.DTOs;
using TaskTracker.Application.Services;
using TaskTracker.Application.Tests.Support;

namespace TaskTracker.Application.Tests;

public sealed class TaskServiceCreateTests
{
    /// <summary>
    /// Verifica que la creación de tareas recorte espacios y persista los datos normalizados.
    /// </summary>
    /// <returns>Una tarea completada cuando la validación de persistencia finaliza.</returns>
    [Fact]
    public async Task CreateAsync_ShouldTrimText_AndPersistTask()
    {
        var repository = new InMemoryTaskRepository();
        var service = new TaskService(repository);

        var id = await service.CreateAsync(new CreateTaskRequest
        {
            Title = "  Revisar integracion  ",
            Description = "  endpoint /tasks  ",
            DueDate = new DateTime(2026, 03, 20)
        });

        var created = await repository.GetByIdAsync(id);

        Assert.NotNull(created);
        Assert.Equal("Revisar integracion", created!.Title);
        Assert.Equal("endpoint /tasks", created.Description);
        Assert.Equal(TaskTracker.Domain.Enums.TaskStatus.Created, created.Status);
    }

    /// <summary>
    /// Verifica que las etiquetas se normalicen eliminando duplicados y vacíos.
    /// </summary>
    /// <returns>Una tarea completada cuando la validación de etiquetas finaliza.</returns>
    [Fact]
    public async Task CreateAsync_ShouldNormalizeLabels()
    {
        var repository = new InMemoryTaskRepository();
        var service = new TaskService(repository);

        var id = await service.CreateAsync(new CreateTaskRequest
        {
            Title = "Agregar etiquetas",
            Labels = [" backend ", "Backend", "kanban", ""]
        });

        var created = await repository.GetByIdAsync(id);

        Assert.NotNull(created);
        Assert.Equal(2, created!.Labels.Count);
        Assert.True(created.Labels.Any(label => string.Equals(label, "backend", StringComparison.OrdinalIgnoreCase)));
        Assert.True(created.Labels.Any(label => string.Equals(label, "kanban", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Verifica que la creación falle cuando el título está vacío.
    /// </summary>
    /// <returns>Una tarea completada cuando la validación del error esperado finaliza.</returns>
    [Fact]
    public async Task CreateAsync_ShouldFail_WhenTitleIsEmpty()
    {
        var service = new TaskService(new InMemoryTaskRepository());

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(new CreateTaskRequest
        {
            Title = " ",
            Description = "sin titulo"
        }));
    }
}
