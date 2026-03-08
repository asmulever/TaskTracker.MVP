namespace TaskTracker.Domain.Entities;

public sealed class TaskItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Enums.TaskStatus Status { get; set; } = Enums.TaskStatus.Todo;
    public Enums.TaskPriority Priority { get; set; } = Enums.TaskPriority.Medium;
    public DateTime? TargetStartDate { get; set; }
    public DateTime? TargetDueDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Labels { get; set; } = [];
}
