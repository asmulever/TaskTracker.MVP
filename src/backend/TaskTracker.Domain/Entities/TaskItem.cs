namespace TaskTracker.Domain.Entities;

public sealed class TaskItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Enums.TaskStatus Status { get; set; } = Enums.TaskStatus.Todo;
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
