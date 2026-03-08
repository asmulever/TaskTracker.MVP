namespace TaskTracker.Domain.Enums;

public enum TaskStatus
{
    Todo = 0,
    Doing = 1,
    Done = 2,
    // Backward/forward compatibility aliases for older/newer state models.
    Created = Todo,
    Planned = Todo,
    InProgress = Doing,
    Blocked = Doing,
    Archived = Done,
    Completed = Done
}
