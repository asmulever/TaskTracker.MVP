using TaskTracker.Application.Abstractions;
using TaskTracker.Application.DTOs;
using TaskTracker.Domain.Entities;
using TaskStatus = TaskTracker.Domain.Enums.TaskStatus;

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
            Status = TaskTracker.Domain.Enums.TaskStatus.Todo,
            Priority = request.Priority,
            TargetStartDate = request.TargetStartDate,
            TargetDueDate = request.TargetDueDate ?? request.DueDate,
            DueDate = request.TargetDueDate ?? request.DueDate,
            CreatedAt = DateTime.UtcNow,
            Labels = NormalizeLabels(request.Labels)
        };

        var id = await taskRepository.CreateAsync(task, cancellationToken);
        await RegisterActivityAsync(id, "TaskCreated", "Task created.", cancellationToken);
        return id;
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
        existing.Labels = NormalizeLabels(request.Labels);

        var updated = await taskRepository.UpdateAsync(existing, cancellationToken);
        if (updated)
        {
            await RegisterActivityAsync(id, "TaskUpdated", "Task data updated.", cancellationToken);
        }

        return updated;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deleted = await taskRepository.DeleteAsync(id, cancellationToken);
        if (deleted)
        {
            await RegisterActivityAsync(id, "TaskDeleted", "Task deleted.", cancellationToken);
        }

        return deleted;
    }

    public async Task<bool> UpdateStatusAsync(Guid id, UpdateTaskStatusRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await taskRepository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        if (!CanTransition(existing.Status, request.Status))
        {
            throw new ArgumentException($"Invalid transition from {existing.Status} to {request.Status}.", nameof(request.Status));
        }

        var changed = await taskRepository.UpdateStatusAsync(id, request.Status, cancellationToken);
        if (changed)
        {
            await RegisterActivityAsync(
                id,
                "StatusChanged",
                $"Status changed from {existing.Status} to {request.Status}.",
                cancellationToken);
        }

        return changed;
    }

    public Task<IReadOnlyCollection<TaskComment>> GetCommentsAsync(Guid taskId, CancellationToken cancellationToken = default)
        => taskRepository.GetCommentsAsync(taskId, cancellationToken);

    public Task<IReadOnlyCollection<TaskActivity>> GetActivityAsync(Guid taskId, CancellationToken cancellationToken = default)
        => taskRepository.GetActivityAsync(taskId, cancellationToken);

    public async Task<Guid?> AddCommentAsync(Guid taskId, CreateTaskCommentRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ArgumentException("Comment content is required.", nameof(request.Content));
        }

        var comment = new TaskComment
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            Content = request.Content.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        var commentId = await taskRepository.AddCommentAsync(comment, cancellationToken);
        if (commentId is not null)
        {
            await RegisterActivityAsync(taskId, "CommentAdded", "Comment added.", cancellationToken);
        }

        return commentId;
    }

    private Task RegisterActivityAsync(Guid taskId, string action, string detail, CancellationToken cancellationToken)
    {
        return taskRepository.AddActivityAsync(new TaskActivity
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            Action = action,
            Detail = detail,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
    }

    private static bool CanTransition(TaskStatus current, TaskStatus next)
    {
        _ = current;
        _ = next;
        // Restriction removed: UI handles warning/confirmation for backward moves.
        return true;
    }

    private static List<string> NormalizeLabels(IReadOnlyCollection<string>? labels)
    {
        if (labels is null || labels.Count == 0)
        {
            return [];
        }

        return labels
            .Select(static label => (label ?? string.Empty).Trim())
            .Where(static label => label.Length > 0)
            .Select(static label => label.Length > 30 ? label[..30] : label)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();
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
