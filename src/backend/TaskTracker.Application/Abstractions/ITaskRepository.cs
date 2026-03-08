using TaskTracker.Domain.Entities;
namespace TaskTracker.Application.Abstractions;

public interface ITaskRepository
{
    /// <summary>
    /// Obtiene todas las tareas persistidas.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>La colección completa de tareas almacenadas.</returns>
    Task<IReadOnlyCollection<TaskItem>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca una tarea por identificador dentro del almacenamiento.
    /// </summary>
    /// <param name="id">Identificador de la tarea a buscar.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>La tarea encontrada o <see langword="null"/> cuando no existe.</returns>
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste una nueva tarea.
    /// </summary>
    /// <param name="task">Entidad de tarea a almacenar.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>El identificador de la tarea creada.</returns>
    Task<Guid> CreateAsync(TaskItem task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza una tarea existente en el almacenamiento.
    /// </summary>
    /// <param name="task">Entidad de tarea con los valores actualizados.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns><see langword="true"/> si la tarea fue actualizada; en caso contrario, <see langword="false"/>.</returns>
    Task<bool> UpdateAsync(TaskItem task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina una tarea persistida por su identificador.
    /// </summary>
    /// <param name="id">Identificador de la tarea a eliminar.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns><see langword="true"/> si la tarea fue eliminada; en caso contrario, <see langword="false"/>.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza el estado persistido de una tarea.
    /// </summary>
    /// <param name="id">Identificador de la tarea cuyo estado se modificará.</param>
    /// <param name="status">Nuevo estado que debe guardarse.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns><see langword="true"/> si el estado fue actualizado; en caso contrario, <see langword="false"/>.</returns>
    Task<bool> UpdateStatusAsync(Guid id, Domain.Enums.TaskStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera los comentarios de una tarea.
    /// </summary>
    /// <param name="taskId">Identificador de la tarea asociada a los comentarios.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>La colección de comentarios persistidos para la tarea.</returns>
    Task<IReadOnlyCollection<TaskComment>> GetCommentsAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Agrega un comentario persistido a una tarea.
    /// </summary>
    /// <param name="comment">Comentario que se desea almacenar.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>El identificador del comentario creado o <see langword="null"/> si la tarea asociada no existe.</returns>
    Task<Guid?> AddCommentAsync(TaskComment comment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene la actividad registrada para una tarea puntual.
    /// </summary>
    /// <param name="taskId">Identificador de la tarea cuya actividad se consultará.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>La colección de eventos de actividad de la tarea.</returns>
    Task<IReadOnlyCollection<TaskActivity>> GetActivityAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene la actividad reciente del sistema a partir de una fecha.
    /// </summary>
    /// <param name="fromUtc">Fecha mínima en UTC para filtrar la actividad.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>La colección de eventos de actividad ocurridos desde la fecha indicada.</returns>
    Task<IReadOnlyCollection<TaskActivity>> GetRecentActivityFeedAsync(DateTime fromUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste un evento de actividad para una tarea.
    /// </summary>
    /// <param name="activity">Evento de actividad a almacenar.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>Una tarea completada cuando la actividad fue guardada.</returns>
    Task AddActivityAsync(TaskActivity activity, CancellationToken cancellationToken = default);
}
