using TaskTracker.Application.DTOs;
using TaskTracker.Application.Services;
using TaskTracker.Application.Tests.Support;

namespace TaskTracker.Application.Tests;

public sealed class TaskServiceCommentsTests
{
    [Fact]
    public async Task AddCommentAsync_ShouldPersistComment()
    {
        var repository = new InMemoryTaskRepository();
        var service = new TaskService(repository);
        var taskId = await service.CreateAsync(new CreateTaskRequest { Title = "Agregar comentarios" });

        var commentId = await service.AddCommentAsync(taskId, new CreateTaskCommentRequest
        {
            Content = " Primer comentario "
        });

        var comments = await service.GetCommentsAsync(taskId);

        Assert.NotNull(commentId);
        Assert.Single(comments);
        Assert.Equal("Primer comentario", comments.First().Content);
    }

    [Fact]
    public async Task AddCommentAsync_ShouldPersistImageAttachment()
    {
        var repository = new InMemoryTaskRepository();
        var service = new TaskService(repository);
        var taskId = await service.CreateAsync(new CreateTaskRequest { Title = "Comentario con imagen" });

        var commentId = await service.AddCommentAsync(taskId, new CreateTaskCommentRequest
        {
            Content = string.Empty,
            ImageDataUrl = "data:image/png;base64,AAAA",
            ImageFileName = "captura.png"
        });

        var comments = await service.GetCommentsAsync(taskId);

        Assert.NotNull(commentId);
        Assert.Single(comments);
        Assert.Equal("data:image/png;base64,AAAA", comments.First().ImageDataUrl);
        Assert.Equal("captura.png", comments.First().ImageFileName);
    }

    [Fact]
    public async Task AddCommentAsync_ShouldReturnNull_WhenTaskDoesNotExist()
    {
        var service = new TaskService(new InMemoryTaskRepository());

        var commentId = await service.AddCommentAsync(Guid.NewGuid(), new CreateTaskCommentRequest
        {
            Content = "No existe"
        });

        Assert.Null(commentId);
    }

    [Fact]
    public async Task AddCommentAsync_ShouldRejectEmptyCommentWithoutImage()
    {
        var repository = new InMemoryTaskRepository();
        var service = new TaskService(repository);
        var taskId = await service.CreateAsync(new CreateTaskRequest { Title = "Validar comentario" });

        await Assert.ThrowsAsync<ArgumentException>(() => service.AddCommentAsync(taskId, new CreateTaskCommentRequest()));
    }
}
