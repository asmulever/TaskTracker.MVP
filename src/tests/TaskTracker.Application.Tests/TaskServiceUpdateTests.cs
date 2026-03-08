using TaskTracker.Application.DTOs;
using TaskTracker.Application.Services;
using TaskTracker.Application.Tests.Support;

namespace TaskTracker.Application.Tests;

public sealed class TaskServiceUpdateTests
{
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
