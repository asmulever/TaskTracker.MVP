using TaskTracker.Application.Abstractions;
using TaskTracker.Application.DTOs;
using TaskTracker.Domain.Entities;

namespace TaskTracker.Application.Services;

public sealed class TaskService(ITaskRepository taskRepository) : ITaskService
{
    public Task<IReadOnlyCollection<TaskItem>> GetAllAsync(CancellationToken cancellationToken = default)
        => taskRepository.GetAllAsync(cancellationToken);

    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => taskRepository.GetByIdAsync(id, cancellationToken);

    public async Task<Guid> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        ValidateTitle(request.Title);

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Status = TaskTracker.Domain.Enums.TaskStatus.Created,
            Priority = request.Priority,
            TargetStartDate = request.TargetStartDate,
            TargetDueDate = request.TargetDueDate ?? request.DueDate,
            DueDate = request.TargetDueDate ?? request.DueDate,
            CreatedAt = DateTime.UtcNow
        };

        return await taskRepository.CreateAsync(task, cancellationToken);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateTaskRequest request, CancellationToken cancellationToken = default)
    {
        ValidateTitle(request.Title);

        var existing = await taskRepository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        existing.Title = request.Title.Trim();
        existing.Description = request.Description?.Trim() ?? string.Empty;
        existing.Priority = request.Priority;
        existing.TargetStartDate = request.TargetStartDate;
        existing.TargetDueDate = request.TargetDueDate ?? request.DueDate;
        existing.DueDate = request.TargetDueDate ?? request.DueDate;

        return await taskRepository.UpdateAsync(existing, cancellationToken);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => taskRepository.DeleteAsync(id, cancellationToken);

    public async Task<bool> UpdateStatusAsync(Guid id, UpdateTaskStatusRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await taskRepository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        return await taskRepository.UpdateStatusAsync(id, request.Status, cancellationToken);
    }

    private static void ValidateTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        if (title.Trim().Length > 200)
        {
            throw new ArgumentException("Title cannot exceed 200 characters.", nameof(title));
        }
    }
}
