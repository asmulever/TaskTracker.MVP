namespace TaskTracker.Application.DTOs;

public sealed class UpdateTaskStatusRequest
{
    public Domain.Enums.TaskStatus Status { get; set; }
}
