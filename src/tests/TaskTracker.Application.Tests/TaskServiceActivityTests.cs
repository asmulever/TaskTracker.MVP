using TaskTracker.Application.DTOs;
using TaskTracker.Application.Services;
using TaskTracker.Application.Tests.Support;

namespace TaskTracker.Application.Tests;

public sealed class TaskServiceActivityTests
{
    /// <summary>
    /// Verifica que la actividad registre la creación de la tarea y el posterior cambio de estado.
    /// </summary>
    /// <returns>Una tarea completada cuando la validación del caso de prueba finaliza.</returns>
    [Fact]
    public async Task Activity_ShouldTrackCreateAndStatusChange()
    {
        var repository = new InMemoryTaskRepository();
        var service = new TaskService(repository);

        var taskId = await service.CreateAsync(new CreateTaskRequest
        {
            Title = "Actividad"
        });

        await service.UpdateStatusAsync(taskId, new UpdateTaskStatusRequest
        {
            Status = TaskTracker.Domain.Enums.TaskStatus.Planned
        });

        var activity = await service.GetActivityAsync(taskId);

        Assert.True(activity.Count >= 2);
        Assert.Contains(activity, item => item.Action == "TaskCreated");
        Assert.Contains(activity, item => item.Action == "StatusChanged");
    }
}
