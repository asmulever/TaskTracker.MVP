using TaskTracker.Application.Abstractions;
using TaskTracker.Domain.Entities;

namespace TaskTracker.Application.Tests.Support;

internal sealed class InMemoryTaskRepository : ITaskRepository
{
    private readonly List<TaskItem> _tasks = [];
    private readonly List<TaskComment> _comments = [];
    private readonly List<TaskActivity> _activity = [];

    public Task<IReadOnlyCollection<TaskItem>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult((IReadOnlyCollection<TaskItem>)_tasks.OrderByDescending(t => t.CreatedAt).ToList());

    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_tasks.FirstOrDefault(t => t.Id == id));

    public Task<Guid> CreateAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        _tasks.Add(task);
        return Task.FromResult(task.Id);
    }

    public Task<bool> UpdateAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        var current = _tasks.FirstOrDefault(t => t.Id == task.Id);
        if (current is null)
        {
            return Task.FromResult(false);
        }

        current.Title = task.Title;
        current.Description = task.Description;
        current.Priority = task.Priority;
        current.TargetStartDate = task.TargetStartDate;
        current.TargetDueDate = task.TargetDueDate;
        current.DueDate = task.DueDate;
        current.Labels = [..task.Labels];
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var removed = _tasks.RemoveAll(t => t.Id == id) > 0;
        return Task.FromResult(removed);
    }

    public Task<bool> UpdateStatusAsync(Guid id, TaskTracker.Domain.Enums.TaskStatus status, CancellationToken cancellationToken = default)
    {
        var current = _tasks.FirstOrDefault(t => t.Id == id);
        if (current is null)
        {
            return Task.FromResult(false);
        }

        current.Status = status;
        return Task.FromResult(true);
    }

    public Task<IReadOnlyCollection<TaskComment>> GetCommentsAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var comments = _comments
            .Where(comment => comment.TaskId == taskId)
            .OrderBy(comment => comment.CreatedAt)
            .ToList();
        return Task.FromResult((IReadOnlyCollection<TaskComment>)comments);
    }

    public Task<Guid?> AddCommentAsync(TaskComment comment, CancellationToken cancellationToken = default)
    {
        if (_tasks.All(task => task.Id != comment.TaskId))
        {
            return Task.FromResult<Guid?>(null);
        }

        _comments.Add(comment);
        return Task.FromResult<Guid?>(comment.Id);
    }

    public Task<IReadOnlyCollection<TaskActivity>> GetActivityAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var activity = _activity
            .Where(item => item.TaskId == taskId)
            .OrderByDescending(item => item.CreatedAt)
            .ToList();
        return Task.FromResult((IReadOnlyCollection<TaskActivity>)activity);
    }

    public Task<IReadOnlyCollection<TaskActivity>> GetRecentActivityFeedAsync(DateTime fromUtc, CancellationToken cancellationToken = default)
    {
        var activity = _activity
            .Where(item => item.CreatedAt >= fromUtc)
            .OrderByDescending(item => item.CreatedAt)
            .ToList();
        return Task.FromResult((IReadOnlyCollection<TaskActivity>)activity);
    }

    public Task AddActivityAsync(TaskActivity activity, CancellationToken cancellationToken = default)
    {
        _activity.Add(activity);
        return Task.CompletedTask;
    }
}
