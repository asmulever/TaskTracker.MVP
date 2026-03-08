namespace TaskTracker.Domain.Entities;

public sealed class TaskActivity
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
