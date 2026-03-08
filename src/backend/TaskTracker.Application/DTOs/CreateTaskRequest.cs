namespace TaskTracker.Application.DTOs;

public sealed class CreateTaskRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Domain.Enums.TaskPriority Priority { get; set; } = Domain.Enums.TaskPriority.Medium;
    public DateTime? TargetStartDate { get; set; }
    public DateTime? TargetDueDate { get; set; }
    public DateTime? DueDate { get; set; }
}
