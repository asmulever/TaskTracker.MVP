using TaskTracker.Application.DTOs;
using TaskTracker.Domain.Entities;

namespace TaskTracker.Application.Abstractions;

public interface ITaskService
{
    /// <summary>
    /// Obtiene la colección completa de tareas disponibles.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>Una colección de tareas ordenadas según la implementación concreta.</returns>
    Task<IReadOnlyCollection<TaskItem>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca una tarea por su identificador único.
    /// </summary>
    /// <param name="id">Identificador de la tarea a recuperar.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>La tarea encontrada o <see langword="null"/> cuando no existe.</returns>
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea una nueva tarea a partir de los datos recibidos.
    /// </summary>
    /// <param name="request">Datos necesarios para crear la tarea.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>El identificador generado para la nueva tarea.</returns>
    Task<Guid> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza los datos editables de una tarea existente.
    /// </summary>
    /// <param name="id">Identificador de la tarea a modificar.</param>
    /// <param name="request">Datos nuevos que se aplicarán sobre la tarea.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns><see langword="true"/> si la tarea fue actualizada; en caso contrario, <see langword="false"/>.</returns>
    Task<bool> UpdateAsync(Guid id, UpdateTaskRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina una tarea existente.
    /// </summary>
    /// <param name="id">Identificador de la tarea a eliminar.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns><see langword="true"/> si la tarea fue eliminada; en caso contrario, <see langword="false"/>.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambia el estado operativo de una tarea.
    /// </summary>
    /// <param name="id">Identificador de la tarea cuyo estado se actualizará.</param>
    /// <param name="request">Nuevo estado solicitado para la tarea.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns><see langword="true"/> si el estado cambió correctamente; en caso contrario, <see langword="false"/>.</returns>
    Task<bool> UpdateStatusAsync(Guid id, UpdateTaskStatusRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera los comentarios asociados a una tarea.
    /// </summary>
    /// <param name="taskId">Identificador de la tarea cuyos comentarios se consultarán.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>La colección de comentarios registrados para la tarea indicada.</returns>
    Task<IReadOnlyCollection<TaskComment>> GetCommentsAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Agrega un comentario opcionalmente acompañado por una imagen a una tarea.
    /// </summary>
    /// <param name="taskId">Identificador de la tarea a la que se agregará el comentario.</param>
    /// <param name="request">Contenido del comentario y datos del adjunto.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>El identificador del comentario creado o <see langword="null"/> si la tarea no existe.</returns>
    Task<Guid?> AddCommentAsync(Guid taskId, CreateTaskCommentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene la actividad histórica registrada para una tarea.
    /// </summary>
    /// <param name="taskId">Identificador de la tarea cuya actividad se consultará.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>La colección de eventos de actividad asociados a la tarea.</returns>
    Task<IReadOnlyCollection<TaskActivity>> GetActivityAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera la actividad reciente del tablero a partir de una fecha dada.
    /// </summary>
    /// <param name="fromUtc">Fecha mínima en UTC desde la cual traer actividad.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>La colección de eventos recientes ocurridos desde la fecha indicada.</returns>
    Task<IReadOnlyCollection<TaskActivity>> GetRecentActivityFeedAsync(DateTime fromUtc, CancellationToken cancellationToken = default);
}
