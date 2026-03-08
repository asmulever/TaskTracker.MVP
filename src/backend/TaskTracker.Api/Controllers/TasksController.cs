using Microsoft.AspNetCore.Mvc;
using TaskTracker.Application.Abstractions;
using TaskTracker.Application.DTOs;
using TaskTracker.Api.Contracts;

namespace TaskTracker.Api.Controllers;

[ApiController]
[Route("tasks")]
public sealed class TasksController(ITaskService taskService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var tasks = await taskService.GetAllAsync(cancellationToken);
        return Ok(tasks);
    }

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

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await taskService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

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

    [HttpGet("activity-feed")]
    public async Task<IActionResult> GetActivityFeed([FromQuery] DateTime? fromUtc, CancellationToken cancellationToken)
    {
        var activity = await taskService.GetRecentActivityFeedAsync(
            (fromUtc ?? DateTime.UtcNow.AddDays(-7)).ToUniversalTime(),
            cancellationToken);

        return Ok(activity);
    }

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
