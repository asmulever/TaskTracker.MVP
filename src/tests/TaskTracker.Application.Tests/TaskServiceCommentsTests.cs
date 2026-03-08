using TaskTracker.Application.DTOs;
using TaskTracker.Application.Services;
using TaskTracker.Application.Tests.Support;

namespace TaskTracker.Application.Tests;

public sealed class TaskServiceCommentsTests
{
    /// <summary>
    /// Verifica que un comentario textual se persista correctamente.
    /// </summary>
    /// <returns>Una tarea completada cuando la validación del comentario finaliza.</returns>
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

    /// <summary>
    /// Verifica que un comentario con imagen adjunta se persista correctamente.
    /// </summary>
    /// <returns>Una tarea completada cuando la validación del adjunto finaliza.</returns>
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

    /// <summary>
    /// Verifica que agregar un comentario sobre una tarea inexistente no genere un identificador.
    /// </summary>
    /// <returns>Una tarea completada cuando la validación del caso inexistente finaliza.</returns>
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

    /// <summary>
    /// Verifica que no se permitan comentarios vacíos cuando tampoco se adjunta una imagen.
    /// </summary>
    /// <returns>Una tarea completada cuando la validación del error esperado finaliza.</returns>
    [Fact]
    public async Task AddCommentAsync_ShouldRejectEmptyCommentWithoutImage()
    {
        var repository = new InMemoryTaskRepository();
        var service = new TaskService(repository);
        var taskId = await service.CreateAsync(new CreateTaskRequest { Title = "Validar comentario" });

        await Assert.ThrowsAsync<ArgumentException>(() => service.AddCommentAsync(taskId, new CreateTaskCommentRequest()));
    }

    /// <summary>
    /// Verifica que se rechacen imágenes que superan el límite máximo permitido.
    /// </summary>
    /// <returns>Una tarea completada cuando la validación del límite de tamaño finaliza.</returns>
    [Fact]
    public async Task AddCommentAsync_ShouldRejectImageLargerThan2Mb()
    {
        var repository = new InMemoryTaskRepository();
        var service = new TaskService(repository);
        var taskId = await service.CreateAsync(new CreateTaskRequest { Title = "Validar imagen" });
        var oversizedBytes = (2 * 1024 * 1024) + 3;
        var base64Length = ((oversizedBytes + 2) / 3) * 4;
        var imageDataUrl = $"data:image/png;base64,{new string('A', base64Length)}";

        await Assert.ThrowsAsync<ArgumentException>(() => service.AddCommentAsync(taskId, new CreateTaskCommentRequest
        {
            ImageDataUrl = imageDataUrl,
            ImageFileName = "oversized.png"
        }));
    }
}
