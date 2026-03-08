using TaskTracker.Application.DTOs;
using TaskTracker.Domain.Entities;

namespace TaskTracker.Application.Abstractions;

public interface ITaskService
{
    Task<IReadOnlyCollection<TaskItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Guid id, UpdateTaskRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> UpdateStatusAsync(Guid id, UpdateTaskStatusRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<TaskComment>> GetCommentsAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task<Guid?> AddCommentAsync(Guid taskId, CreateTaskCommentRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<TaskActivity>> GetActivityAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<TaskActivity>> GetRecentActivityFeedAsync(DateTime fromUtc, CancellationToken cancellationToken = default);
}
