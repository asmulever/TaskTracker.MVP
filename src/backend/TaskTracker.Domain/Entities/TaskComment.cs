namespace TaskTracker.Domain.Entities;

public sealed class TaskComment
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
