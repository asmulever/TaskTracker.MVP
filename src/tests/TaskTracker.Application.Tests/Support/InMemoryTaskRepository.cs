using TaskTracker.Application.Abstractions;
using TaskTracker.Domain.Entities;

namespace TaskTracker.Application.Tests.Support;

internal sealed class InMemoryTaskRepository : ITaskRepository
{
    private readonly List<TaskItem> _tasks = [];

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
}
