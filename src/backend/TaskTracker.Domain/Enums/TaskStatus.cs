namespace TaskTracker.Domain.Enums;

public enum TaskStatus
{
    Created = 0,
    Planned = 1,
    InProgress = 2,
    Blocked = 3,
    Done = 4,
    Archived = 5,
    Todo = Created,
    Doing = InProgress
}
