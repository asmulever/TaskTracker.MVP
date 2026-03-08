using TaskTracker.Application.Abstractions;
using TaskTracker.Domain.Entities;

namespace TaskTracker.Application.Tests.Support;

internal sealed class InMemoryTaskRepository : ITaskRepository
{
    private readonly List<TaskItem> _tasks = [];
    private readonly List<TaskComment> _comments = [];
    private readonly List<TaskActivity> _activity = [];

    /// <summary>
    /// Devuelve todas las tareas cargadas en memoria ordenadas por fecha de creación descendente.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>La colección de tareas actualmente almacenadas en memoria.</returns>
    public Task<IReadOnlyCollection<TaskItem>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult((IReadOnlyCollection<TaskItem>)_tasks.OrderByDescending(t => t.CreatedAt).ToList());

    /// <summary>
    /// Busca una tarea en memoria por identificador.
    /// </summary>
    /// <param name="id">Identificador de la tarea a buscar.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>La tarea encontrada o <see langword="null"/> cuando no existe.</returns>
    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_tasks.FirstOrDefault(t => t.Id == id));

    /// <summary>
    /// Agrega una tarea al almacenamiento en memoria.
    /// </summary>
    /// <param name="task">Tarea a persistir en memoria.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>El identificador de la tarea agregada.</returns>
    public Task<Guid> CreateAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        _tasks.Add(task);
        return Task.FromResult(task.Id);
    }

    /// <summary>
    /// Actualiza una tarea existente dentro del almacenamiento en memoria.
    /// </summary>
    /// <param name="task">Tarea con los valores actualizados.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns><see langword="true"/> si la tarea fue encontrada y actualizada; en caso contrario, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Elimina una tarea del almacenamiento en memoria.
    /// </summary>
    /// <param name="id">Identificador de la tarea a eliminar.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns><see langword="true"/> si la tarea fue eliminada; en caso contrario, <see langword="false"/>.</returns>
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var removed = _tasks.RemoveAll(t => t.Id == id) > 0;
        return Task.FromResult(removed);
    }

    /// <summary>
    /// Cambia el estado de una tarea dentro del almacenamiento en memoria.
    /// </summary>
    /// <param name="id">Identificador de la tarea a modificar.</param>
    /// <param name="status">Nuevo estado a aplicar.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns><see langword="true"/> si la tarea fue encontrada; en caso contrario, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Obtiene los comentarios en memoria de una tarea específica.
    /// </summary>
    /// <param name="taskId">Identificador de la tarea consultada.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>La colección de comentarios asociados a la tarea.</returns>
    public Task<IReadOnlyCollection<TaskComment>> GetCommentsAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var comments = _comments
            .Where(comment => comment.TaskId == taskId)
            .OrderBy(comment => comment.CreatedAt)
            .ToList();
        return Task.FromResult((IReadOnlyCollection<TaskComment>)comments);
    }

    /// <summary>
    /// Agrega un comentario en memoria cuando la tarea relacionada existe.
    /// </summary>
    /// <param name="comment">Comentario a almacenar.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>El identificador del comentario o <see langword="null"/> si la tarea no existe.</returns>
    public Task<Guid?> AddCommentAsync(TaskComment comment, CancellationToken cancellationToken = default)
    {
        if (_tasks.All(task => task.Id != comment.TaskId))
        {
            return Task.FromResult<Guid?>(null);
        }

        _comments.Add(comment);
        return Task.FromResult<Guid?>(comment.Id);
    }

    /// <summary>
    /// Obtiene la actividad asociada a una tarea dentro del almacenamiento en memoria.
    /// </summary>
    /// <param name="taskId">Identificador de la tarea consultada.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>La colección de actividad de la tarea.</returns>
    public Task<IReadOnlyCollection<TaskActivity>> GetActivityAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var activity = _activity
            .Where(item => item.TaskId == taskId)
            .OrderByDescending(item => item.CreatedAt)
            .ToList();
        return Task.FromResult((IReadOnlyCollection<TaskActivity>)activity);
    }

    /// <summary>
    /// Obtiene el feed de actividad en memoria desde una fecha mínima.
    /// </summary>
    /// <param name="fromUtc">Fecha mínima en UTC para incluir actividad.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>La colección de actividad reciente almacenada en memoria.</returns>
    public Task<IReadOnlyCollection<TaskActivity>> GetRecentActivityFeedAsync(DateTime fromUtc, CancellationToken cancellationToken = default)
    {
        var activity = _activity
            .Where(item => item.CreatedAt >= fromUtc)
            .OrderByDescending(item => item.CreatedAt)
            .ToList();
        return Task.FromResult((IReadOnlyCollection<TaskActivity>)activity);
    }

    /// <summary>
    /// Registra un evento de actividad en memoria.
    /// </summary>
    /// <param name="activity">Evento de actividad a almacenar.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>Una tarea completada cuando la actividad queda registrada.</returns>
    public Task AddActivityAsync(TaskActivity activity, CancellationToken cancellationToken = default)
    {
        _activity.Add(activity);
        return Task.CompletedTask;
    }
}
