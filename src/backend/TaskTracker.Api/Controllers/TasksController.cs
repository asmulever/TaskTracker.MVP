using Microsoft.AspNetCore.Mvc;
using TaskTracker.Application.Abstractions;
using TaskTracker.Application.DTOs;

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

}
