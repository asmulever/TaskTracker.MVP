using Microsoft.AspNetCore.Mvc;
using TaskTracker.Application.Abstractions;
using TaskTracker.Application.DTOs;
using TaskTracker.Api.Contracts;

namespace TaskTracker.Api.Controllers;

[ApiController]
[Route("tasks")]
public sealed class TasksController(ITaskService taskService) : ControllerBase
{
    /// <summary>
    /// Devuelve el listado completo de tareas.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>Una respuesta HTTP 200 con la colección de tareas.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var tasks = await taskService.GetAllAsync(cancellationToken);
        return Ok(tasks);
    }

    /// <summary>
    /// Recupera una tarea específica por identificador.
    /// </summary>
    /// <param name="id">Identificador de la tarea solicitada.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>Una respuesta HTTP 200 con la tarea o 404 si no existe.</returns>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var task = await taskService.GetByIdAsync(id, cancellationToken);
        if (task is null)
        {
            return NotFound();
        }

        return Ok(task);
    }

    /// <summary>
    /// Crea una nueva tarea en el sistema.
    /// </summary>
    /// <param name="request">Datos utilizados para crear la tarea.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>Una respuesta HTTP 201 con el identificador creado o un error de validación.</returns>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var id = await taskService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id }, new { id });
        }
        catch (ArgumentException exception)
        {
            return ValidationProblem(detail: exception.Message);
        }
    }

    /// <summary>
    /// Actualiza los datos editables de una tarea existente.
    /// </summary>
    /// <param name="id">Identificador de la tarea a modificar.</param>
    /// <param name="request">Nuevos datos de la tarea.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>Una respuesta HTTP 204 si la tarea se actualiza, 404 si no existe o un error de validación.</returns>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await taskService.UpdateAsync(id, request, cancellationToken);
            return updated ? NoContent() : NotFound();
        }
        catch (ArgumentException exception)
        {
            return ValidationProblem(detail: exception.Message);
        }
    }

    /// <summary>
    /// Elimina una tarea por identificador.
    /// </summary>
    /// <param name="id">Identificador de la tarea a eliminar.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>Una respuesta HTTP 204 si se elimina o 404 si no existe.</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await taskService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>
    /// Actualiza el estado de una tarea.
    /// </summary>
    /// <param name="id">Identificador de la tarea cuyo estado se cambiará.</param>
    /// <param name="request">Nuevo estado solicitado.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>Una respuesta HTTP 204 si el estado se actualiza, 404 si no existe o un error de validación.</returns>
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateTaskStatusRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await taskService.UpdateStatusAsync(id, request, cancellationToken);
            return updated ? NoContent() : NotFound();
        }
        catch (ArgumentException exception)
        {
            return ValidationProblem(detail: exception.Message);
        }
    }

    /// <summary>
    /// Recupera los comentarios asociados a una tarea.
    /// </summary>
    /// <param name="id">Identificador de la tarea consultada.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>Una respuesta HTTP 200 con los comentarios o 404 si la tarea no existe.</returns>
    [HttpGet("{id}/comments")]
    public async Task<IActionResult> GetComments(Guid id, CancellationToken cancellationToken)
    {
        var task = await taskService.GetByIdAsync(id, cancellationToken);
        if (task is null)
        {
            return NotFound();
        }

        var comments = await taskService.GetCommentsAsync(id, cancellationToken);
        return Ok(comments);
    }

    /// <summary>
    /// Agrega un comentario a una tarea usando un cuerpo JSON.
    /// </summary>
    /// <param name="id">Identificador de la tarea que recibirá el comentario.</param>
    /// <param name="request">Contenido del comentario y datos opcionales de imagen.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>Una respuesta HTTP 200 con el identificador del comentario o el error correspondiente.</returns>
    [HttpPost("{id}/comments")]
    [Consumes("application/json")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] CreateTaskCommentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var commentId = await taskService.AddCommentAsync(id, request, cancellationToken);
            return commentId is null ? NotFound() : Ok(new { id = commentId.Value });
        }
        catch (ArgumentException exception)
        {
            return ValidationProblem(detail: exception.Message);
        }
    }

    /// <summary>
    /// Agrega un comentario a una tarea usando multipart/form-data.
    /// </summary>
    /// <param name="id">Identificador de la tarea que recibirá el comentario.</param>
    /// <param name="form">Contenido del comentario y archivo de imagen opcional.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>Una respuesta HTTP 200 con el identificador del comentario o el error correspondiente.</returns>
    [HttpPost("{id}/comments")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> AddCommentForm(Guid id, [FromForm] CreateTaskCommentFormRequest form, CancellationToken cancellationToken)
    {
        try
        {
            var request = await MapCommentFormRequestAsync(form, cancellationToken);
            var commentId = await taskService.AddCommentAsync(id, request, cancellationToken);
            return commentId is null ? NotFound() : Ok(new { id = commentId.Value });
        }
        catch (ArgumentException exception)
        {
            return ValidationProblem(detail: exception.Message);
        }
    }

    /// <summary>
    /// Recupera la actividad histórica de una tarea.
    /// </summary>
    /// <param name="id">Identificador de la tarea consultada.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>Una respuesta HTTP 200 con la actividad o 404 si la tarea no existe.</returns>
    [HttpGet("{id}/activity")]
    public async Task<IActionResult> GetActivity(Guid id, CancellationToken cancellationToken)
    {
        var task = await taskService.GetByIdAsync(id, cancellationToken);
        if (task is null)
        {
            return NotFound();
        }

        var activity = await taskService.GetActivityAsync(id, cancellationToken);
        return Ok(activity);
    }

    /// <summary>
    /// Recupera la actividad reciente del tablero desde una fecha determinada.
    /// </summary>
    /// <param name="fromUtc">Fecha mínima en UTC desde la cual consultar actividad.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>Una respuesta HTTP 200 con la actividad reciente.</returns>
    [HttpGet("activity-feed")]
    public async Task<IActionResult> GetActivityFeed([FromQuery] DateTime? fromUtc, CancellationToken cancellationToken)
    {
        var activity = await taskService.GetRecentActivityFeedAsync(
            (fromUtc ?? DateTime.UtcNow.AddDays(-7)).ToUniversalTime(),
            cancellationToken);

        return Ok(activity);
    }

    /// <summary>
    /// Convierte un formulario multipart en un request de comentario compatible con la capa de aplicación.
    /// </summary>
    /// <param name="form">Formulario recibido desde el cliente.</param>
    /// <param name="cancellationToken">Token para cancelar la lectura asincrónica del archivo.</param>
    /// <returns>Un request de comentario con la imagen transformada a data URL cuando corresponde.</returns>
    private static async Task<CreateTaskCommentRequest> MapCommentFormRequestAsync(
        CreateTaskCommentFormRequest form,
        CancellationToken cancellationToken)
    {
        string? imageDataUrl = null;
        string? imageFileName = null;

        if (form.Image is { Length: > 0 } image)
        {
            if (string.IsNullOrWhiteSpace(image.ContentType) ||
                !image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Comment image must be a valid image file.", nameof(form.Image));
            }

            await using var imageStream = image.OpenReadStream();
            using var memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream, cancellationToken);

            imageDataUrl = $"data:{image.ContentType.Trim()};base64,{Convert.ToBase64String(memoryStream.ToArray())}";
            imageFileName = image.FileName;
        }

        return new CreateTaskCommentRequest
        {
            Content = form.Content ?? string.Empty,
            ImageDataUrl = imageDataUrl,
            ImageFileName = imageFileName
        };
    }

}
