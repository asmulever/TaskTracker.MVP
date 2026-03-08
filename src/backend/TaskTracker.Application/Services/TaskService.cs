using TaskTracker.Application.Abstractions;
using TaskTracker.Application.DTOs;
using TaskTracker.Domain.Entities;
using TaskStatus = TaskTracker.Domain.Enums.TaskStatus;

namespace TaskTracker.Application.Services;

public sealed class TaskService(ITaskRepository taskRepository) : ITaskService
{
    private const int MaxCommentLength = 1000;
    private const int MaxCommentImageFileSizeBytes = 2 * 1024 * 1024;
    private const int MaxCommentImageDataLength = 6_000_000;
    private const int MaxCommentImageFileNameLength = 260;

    /// <summary>
    /// Recupera todas las tareas disponibles desde el repositorio.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>La colección de tareas obtenida desde persistencia.</returns>
    public Task<IReadOnlyCollection<TaskItem>> GetAllAsync(CancellationToken cancellationToken = default)
        => taskRepository.GetAllAsync(cancellationToken);

    /// <summary>
    /// Busca una tarea por su identificador.
    /// </summary>
    /// <param name="id">Identificador de la tarea requerida.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>La tarea encontrada o <see langword="null"/> cuando no existe.</returns>
    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => taskRepository.GetByIdAsync(id, cancellationToken);

    /// <summary>
    /// Crea una nueva tarea validando y normalizando sus datos de entrada.
    /// </summary>
    /// <param name="request">Datos utilizados para crear la tarea.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>El identificador asignado a la nueva tarea.</returns>
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

    /// <summary>
    /// Actualiza una tarea existente con los valores editables informados por el cliente.
    /// </summary>
    /// <param name="id">Identificador de la tarea a actualizar.</param>
    /// <param name="request">Datos nuevos para la tarea.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns><see langword="true"/> si la tarea fue actualizada; en caso contrario, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Elimina una tarea y registra la actividad correspondiente cuando la operación tiene éxito.
    /// </summary>
    /// <param name="id">Identificador de la tarea a eliminar.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns><see langword="true"/> si la tarea fue eliminada; en caso contrario, <see langword="false"/>.</returns>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deleted = await taskRepository.DeleteAsync(id, cancellationToken);
        if (deleted)
        {
            await RegisterActivityAsync(id, "TaskDeleted", "Task deleted.", cancellationToken);
        }

        return deleted;
    }

    /// <summary>
    /// Actualiza el estado de una tarea existente y registra el cambio en la actividad.
    /// </summary>
    /// <param name="id">Identificador de la tarea cuyo estado se modificará.</param>
    /// <param name="request">Request con el nuevo estado solicitado.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns><see langword="true"/> si el estado cambió; en caso contrario, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Recupera los comentarios asociados a una tarea.
    /// </summary>
    /// <param name="taskId">Identificador de la tarea consultada.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>La colección de comentarios de la tarea indicada.</returns>
    public Task<IReadOnlyCollection<TaskComment>> GetCommentsAsync(Guid taskId, CancellationToken cancellationToken = default)
        => taskRepository.GetCommentsAsync(taskId, cancellationToken);

    /// <summary>
    /// Recupera la actividad asociada a una tarea.
    /// </summary>
    /// <param name="taskId">Identificador de la tarea consultada.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>La colección de eventos de actividad asociados a la tarea.</returns>
    public Task<IReadOnlyCollection<TaskActivity>> GetActivityAsync(Guid taskId, CancellationToken cancellationToken = default)
        => taskRepository.GetActivityAsync(taskId, cancellationToken);

    /// <summary>
    /// Recupera el feed de actividad reciente a partir de una fecha.
    /// </summary>
    /// <param name="fromUtc">Fecha mínima en UTC desde la cual devolver actividad.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>La colección de actividad ocurrida desde la fecha indicada.</returns>
    public Task<IReadOnlyCollection<TaskActivity>> GetRecentActivityFeedAsync(DateTime fromUtc, CancellationToken cancellationToken = default)
        => taskRepository.GetRecentActivityFeedAsync(fromUtc, cancellationToken);

    /// <summary>
    /// Agrega un comentario a una tarea validando contenido, adjunto y límites permitidos.
    /// </summary>
    /// <param name="taskId">Identificador de la tarea que recibirá el comentario.</param>
    /// <param name="request">Contenido textual y datos del adjunto opcional.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>El identificador del comentario creado o <see langword="null"/> si la tarea no existe.</returns>
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

            if (GetImageByteSize(imageDataUrl) > MaxCommentImageFileSizeBytes)
            {
                throw new ArgumentException("Comment image cannot exceed 2 MB.", nameof(request.ImageDataUrl));
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

    /// <summary>
    /// Registra un evento de actividad asociado a una tarea.
    /// </summary>
    /// <param name="taskId">Identificador de la tarea sobre la que se registra actividad.</param>
    /// <param name="action">Código o nombre de la acción realizada.</param>
    /// <param name="detail">Detalle descriptivo de la actividad.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>Una tarea completada cuando la actividad fue persistida.</returns>
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

    /// <summary>
    /// Evalúa si una transición de estado está permitida por la regla actual del servicio.
    /// </summary>
    /// <param name="current">Estado actual de la tarea.</param>
    /// <param name="next">Estado de destino solicitado.</param>
    /// <returns><see langword="true"/> cuando la transición está permitida.</returns>
    private static bool CanTransition(TaskStatus current, TaskStatus next)
    {
        _ = current;
        _ = next;
        // Restriction removed: UI handles warning/confirmation for backward moves.
        return true;
    }

    /// <summary>
    /// Calcula el tamaño binario aproximado de una imagen codificada como data URL base64.
    /// </summary>
    /// <param name="imageDataUrl">Data URL de la imagen a medir.</param>
    /// <returns>La cantidad aproximada de bytes representados en la cadena.</returns>
    private static int GetImageByteSize(string imageDataUrl)
    {
        var separatorIndex = imageDataUrl.IndexOf(',');
        if (separatorIndex < 0 || separatorIndex == imageDataUrl.Length - 1)
        {
            throw new ArgumentException("Comment image must be a valid image data URL.");
        }

        var base64 = imageDataUrl[(separatorIndex + 1)..].Trim();
        var padding = 0;
        if (base64.EndsWith("==", StringComparison.Ordinal))
        {
            padding = 2;
        }
        else if (base64.EndsWith("=", StringComparison.Ordinal))
        {
            padding = 1;
        }

        return Math.Max(0, (base64.Length * 3 / 4) - padding);
    }

    /// <summary>
    /// Normaliza la lista de etiquetas removiendo vacíos, duplicados y valores fuera de rango.
    /// </summary>
    /// <param name="labels">Colección original de etiquetas.</param>
    /// <returns>Una lista normalizada lista para persistir.</returns>
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

    /// <summary>
    /// Valida que el título requerido exista y respete el largo máximo permitido.
    /// </summary>
    /// <param name="title">Título recibido para validar.</param>
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
