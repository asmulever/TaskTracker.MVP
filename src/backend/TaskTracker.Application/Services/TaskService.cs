using TaskTracker.Application.Abstractions;
using TaskTracker.Application.DTOs;
using TaskTracker.Domain.Entities;
using TaskStatus = TaskTracker.Domain.Enums.TaskStatus;

namespace TaskTracker.Application.Services;

public sealed class TaskService(ITaskRepository taskRepository) : ITaskService
{
    private const int MaxCommentLength = 1000;
    private const int MaxCommentImageDataLength = 6_000_000;
    private const int MaxCommentImageFileNameLength = 260;

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

    public Task<IReadOnlyCollection<TaskActivity>> GetRecentActivityFeedAsync(DateTime fromUtc, CancellationToken cancellationToken = default)
        => taskRepository.GetRecentActivityFeedAsync(fromUtc, cancellationToken);

    public async Task<Guid?> AddCommentAsync(Guid taskId, CreateTaskCommentRequest request, CancellationToken cancellationToken = default)
    {
        var content = request.Content?.Trim() ?? string.Empty;
        var imageDataUrl = request.ImageDataUrl?.Trim();
        var imageFileName = string.IsNullOrWhiteSpace(request.ImageFileName) ? null : request.ImageFileName.Trim();

        if (string.IsNullOrWhiteSpace(content) && string.IsNullOrWhiteSpace(imageDataUrl))
        {
            throw new ArgumentException("Comment content or image is required.", nameof(request.Content));
        }

        if (content.Length > MaxCommentLength)
        {
            throw new ArgumentException("Comment content cannot exceed 1000 characters.", nameof(request.Content));
        }

        if (!string.IsNullOrWhiteSpace(imageDataUrl))
        {
            if (!imageDataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Comment image must be a valid image data URL.", nameof(request.ImageDataUrl));
            }

            if (imageDataUrl.Length > MaxCommentImageDataLength)
            {
                throw new ArgumentException("Comment image is too large.", nameof(request.ImageDataUrl));
            }
        }

        if (imageFileName is not null && imageFileName.Length > MaxCommentImageFileNameLength)
        {
            imageFileName = imageFileName[..MaxCommentImageFileNameLength];
        }

        var comment = new TaskComment
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            Content = content,
            ImageDataUrl = imageDataUrl,
            ImageFileName = imageFileName,
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
