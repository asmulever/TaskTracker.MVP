using TaskTracker.Domain.Entities;
namespace TaskTracker.Application.Abstractions;

public interface ITaskRepository
{
    Task<IReadOnlyCollection<TaskItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(TaskItem task, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(TaskItem task, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> UpdateStatusAsync(Guid id, Domain.Enums.TaskStatus status, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<TaskComment>> GetCommentsAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task<Guid?> AddCommentAsync(TaskComment comment, CancellationToken cancellationToken = default);
}
