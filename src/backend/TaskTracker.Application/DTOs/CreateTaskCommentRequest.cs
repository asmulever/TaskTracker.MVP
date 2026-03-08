namespace TaskTracker.Application.DTOs;

public sealed class CreateTaskCommentRequest
{
    public string Content { get; set; } = string.Empty;
    public string? ImageDataUrl { get; set; }
    public string? ImageFileName { get; set; }
}
