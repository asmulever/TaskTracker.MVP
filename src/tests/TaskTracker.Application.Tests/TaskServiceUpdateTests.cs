using TaskTracker.Application.DTOs;
using TaskTracker.Application.Services;
using TaskTracker.Application.Tests.Support;

namespace TaskTracker.Application.Tests;

public sealed class TaskServiceUpdateTests
{
    /// <summary>
    /// Verifica que una actualización sobre una tarea inexistente devuelva falso.
    /// </summary>
    /// <returns>Una tarea completada cuando la validación del caso inexistente finaliza.</returns>
    [Fact]
    public async Task UpdateAsync_ShouldReturnFalse_WhenTaskDoesNotExist()
    {
        var service = new TaskService(new InMemoryTaskRepository());

        var updated = await service.UpdateAsync(Guid.NewGuid(), new UpdateTaskRequest
        {
            Title = "Nueva tarea",
            Description = "No existe",
            DueDate = DateTime.UtcNow.Date
        });

        Assert.False(updated);
    }

    /// <summary>
    /// Verifica que una tarea pueda avanzar de estado hasta quedar en Doing.
    /// </summary>
    /// <returns>Una tarea completada cuando la validación del cambio de estado finaliza.</returns>
    [Fact]
    public async Task UpdateStatusAsync_ShouldMoveTaskToDoing()
    {
        var repository = new InMemoryTaskRepository();
        var service = new TaskService(repository);

        var id = await service.CreateAsync(new CreateTaskRequest
        {
            Title = "Ajustar tablero"
        });

        var toPlanned = await service.UpdateStatusAsync(id, new UpdateTaskStatusRequest
        {
            Status = TaskTracker.Domain.Enums.TaskStatus.Planned
        });

        var changed = await service.UpdateStatusAsync(id, new UpdateTaskStatusRequest
        {
            Status = TaskTracker.Domain.Enums.TaskStatus.Doing
        });

        var saved = await repository.GetByIdAsync(id);

        Assert.True(toPlanned);
        Assert.True(changed);
        Assert.NotNull(saved);
        Assert.Equal(TaskTracker.Domain.Enums.TaskStatus.InProgress, saved!.Status);
    }

    /// <summary>
    /// Verifica que una transición inválida de estado dispare la excepción esperada.
    /// </summary>
    /// <returns>Una tarea completada cuando la validación del error esperado finaliza.</returns>
    [Fact]
    public async Task UpdateStatusAsync_ShouldRejectInvalidTransition()
    {
        var repository = new InMemoryTaskRepository();
        var service = new TaskService(repository);

        var id = await service.CreateAsync(new CreateTaskRequest
        {
            Title = "No puede ir directo a Done"
        });

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateStatusAsync(id, new UpdateTaskStatusRequest
        {
            Status = TaskTracker.Domain.Enums.TaskStatus.Done
        }));
    }
}
