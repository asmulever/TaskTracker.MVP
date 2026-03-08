using Microsoft.AspNetCore.Http;

namespace TaskTracker.Api.Contracts;

public sealed class CreateTaskCommentFormRequest
{
    public string? Content { get; set; }
    public IFormFile? Image { get; set; }
}
