using Dapper;
using TaskTracker.Application.Abstractions;
using TaskTracker.Domain.Entities;
using DomainTaskStatus = TaskTracker.Domain.Enums.TaskStatus;

namespace TaskTracker.Infrastructure.Persistence;

public sealed class TaskRepository(ISqlConnectionFactory connectionFactory) : ITaskRepository
{
    public async Task<IReadOnlyCollection<TaskItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id,
                   Title,
                   Description,
                   Status,
                   DueDate,
                   CreatedAt
            FROM dbo.Tasks
            ORDER BY CreatedAt DESC;
            """;

        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<TaskRecord>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.Select(Map).ToArray();
    }

    public async Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id,
                   Title,
                   Description,
                   Status,
                   DueDate,
                   CreatedAt
            FROM dbo.Tasks
            WHERE Id = @Id;
            """;

        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<TaskRecord>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));

        return row is null ? null : Map(row);
    }

    public async Task<Guid> CreateAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO dbo.Tasks (Id, Title, Description, Status, DueDate, CreatedAt)
            VALUES (@Id, @Title, @Description, @Status, @DueDate, @CreatedAt);
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                task.Id,
                task.Title,
                task.Description,
                Status = task.Status.ToString(),
                task.DueDate,
                task.CreatedAt
            },
            cancellationToken: cancellationToken));

        return task.Id;
    }

    public async Task<bool> UpdateAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.Tasks
            SET Title = @Title,
                Description = @Description,
                DueDate = @DueDate
            WHERE Id = @Id;
            """;

        using var connection = connectionFactory.CreateConnection();
        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                task.Id,
                task.Title,
                task.Description,
                task.DueDate
            },
            cancellationToken: cancellationToken));

        return affectedRows > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM dbo.Tasks WHERE Id = @Id;";

        using var connection = connectionFactory.CreateConnection();
        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { Id = id },
            cancellationToken: cancellationToken));

        return affectedRows > 0;
    }

    public async Task<bool> UpdateStatusAsync(Guid id, DomainTaskStatus status, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.Tasks
            SET Status = @Status
            WHERE Id = @Id;
            """;

        using var connection = connectionFactory.CreateConnection();
        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { Id = id, Status = status.ToString() },
            cancellationToken: cancellationToken));

        return affectedRows > 0;
    }

    private static TaskItem Map(TaskRecord record)
    {
        if (!Enum.TryParse<DomainTaskStatus>(record.Status, ignoreCase: true, out var status))
        {
            status = DomainTaskStatus.Todo;
        }

        return new TaskItem
        {
            Id = record.Id,
            Title = record.Title,
            Description = record.Description,
            Status = status,
            DueDate = record.DueDate,
            CreatedAt = record.CreatedAt
        };
    }

    private sealed class TaskRecord
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public DateTime? DueDate { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
