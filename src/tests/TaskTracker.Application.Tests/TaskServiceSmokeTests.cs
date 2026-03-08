using TaskTracker.Application.Services;
using TaskTracker.Application.Tests.Support;

namespace TaskTracker.Application.Tests;

public sealed class TaskServiceSmokeTests
{
    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyCollection_WhenNoData()
    {
        var service = new TaskService(new InMemoryTaskRepository());

        var tasks = await service.GetAllAsync();

        Assert.Empty(tasks);
    }
}
