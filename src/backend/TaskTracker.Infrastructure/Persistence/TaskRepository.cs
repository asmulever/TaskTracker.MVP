using Dapper;
using TaskTracker.Application.Abstractions;
using TaskTracker.Domain.Entities;
using DomainTaskPriority = TaskTracker.Domain.Enums.TaskPriority;
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
                   Priority,
                   TargetStartDate,
                   TargetDueDate,
                   DueDate,
                   CreatedAt
            FROM dbo.Tasks
            ORDER BY CreatedAt DESC;
            """;

        using var connection = connectionFactory.CreateConnection();
        var rows = (await connection.QueryAsync<TaskRecord>(new CommandDefinition(sql, cancellationToken: cancellationToken))).ToArray();
        var labelsByTaskId = await LoadLabelsByTaskIdAsync(connection, rows.Select(row => row.Id).ToArray(), cancellationToken);
        return rows.Select(row => Map(row, labelsByTaskId.GetValueOrDefault(row.Id, []))).ToArray();
    }

    public async Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id,
                   Title,
                   Description,
                   Status,
                   Priority,
                   TargetStartDate,
                   TargetDueDate,
                   DueDate,
                   CreatedAt
            FROM dbo.Tasks
            WHERE Id = @Id;
            """;

        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<TaskRecord>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        var labelsByTaskId = await LoadLabelsByTaskIdAsync(connection, [row.Id], cancellationToken);
        return Map(row, labelsByTaskId.GetValueOrDefault(row.Id, []));
    }

    public async Task<Guid> CreateAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO dbo.Tasks (Id, Title, Description, Status, Priority, TargetStartDate, TargetDueDate, DueDate, CreatedAt)
            VALUES (@Id, @Title, @Description, @Status, @Priority, @TargetStartDate, @TargetDueDate, @DueDate, @CreatedAt);
            """;

        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                task.Id,
                task.Title,
                task.Description,
                Status = task.Status.ToString(),
                Priority = task.Priority.ToString(),
                task.TargetStartDate,
                task.TargetDueDate,
                task.DueDate,
                task.CreatedAt
            },
            transaction: transaction,
            cancellationToken: cancellationToken));

        await ReplaceLabelsAsync(connection, transaction, task.Id, task.Labels, cancellationToken);
        transaction.Commit();
        return task.Id;
    }

    public async Task<bool> UpdateAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.Tasks
            SET Title = @Title,
                Description = @Description,
                Priority = @Priority,
                TargetStartDate = @TargetStartDate,
                TargetDueDate = @TargetDueDate,
                DueDate = @DueDate
            WHERE Id = @Id;
            """;

        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                task.Id,
                task.Title,
                task.Description,
                Priority = task.Priority.ToString(),
                task.TargetStartDate,
                task.TargetDueDate,
                task.DueDate
            },
            transaction: transaction,
            cancellationToken: cancellationToken));

        if (affectedRows > 0)
        {
            await ReplaceLabelsAsync(connection, transaction, task.Id, task.Labels, cancellationToken);
        }

        transaction.Commit();
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

    public async Task<IReadOnlyCollection<TaskComment>> GetCommentsAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, TaskId, Content, CreatedAt
            FROM dbo.TaskComments
            WHERE TaskId = @TaskId
            ORDER BY CreatedAt ASC;
            """;

        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<TaskCommentRecord>(
            new CommandDefinition(sql, new { TaskId = taskId }, cancellationToken: cancellationToken));

        return rows.Select(static row => new TaskComment
        {
            Id = row.Id,
            TaskId = row.TaskId,
            Content = row.Content,
            CreatedAt = row.CreatedAt
        }).ToArray();
    }

    public async Task<Guid?> AddCommentAsync(TaskComment comment, CancellationToken cancellationToken = default)
    {
        const string taskExistsSql = "SELECT 1 FROM dbo.Tasks WHERE Id = @TaskId;";
        const string insertSql = """
            INSERT INTO dbo.TaskComments (Id, TaskId, Content, CreatedAt)
            VALUES (@Id, @TaskId, @Content, @CreatedAt);
            """;

        using var connection = connectionFactory.CreateConnection();
        var exists = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(taskExistsSql, new { TaskId = comment.TaskId }, cancellationToken: cancellationToken));

        if (exists is null)
        {
            return null;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            insertSql,
            new
            {
                comment.Id,
                comment.TaskId,
                comment.Content,
                comment.CreatedAt
            },
            cancellationToken: cancellationToken));

        return comment.Id;
    }

    private static TaskItem Map(TaskRecord record, List<string> labels)
    {
        if (!Enum.TryParse<DomainTaskStatus>(record.Status, ignoreCase: true, out var status))
        {
            status = DomainTaskStatus.Created;
        }

        if (!Enum.TryParse<DomainTaskPriority>(record.Priority, ignoreCase: true, out var priority))
        {
            priority = DomainTaskPriority.Medium;
        }

        return new TaskItem
        {
            Id = record.Id,
            Title = record.Title,
            Description = record.Description,
            Status = status,
            Priority = priority,
            TargetStartDate = record.TargetStartDate,
            TargetDueDate = record.TargetDueDate,
            DueDate = record.DueDate,
            CreatedAt = record.CreatedAt,
            Labels = labels
        };
    }

    private static async Task<Dictionary<Guid, List<string>>> LoadLabelsByTaskIdAsync(
        System.Data.IDbConnection connection,
        IReadOnlyCollection<Guid> taskIds,
        CancellationToken cancellationToken)
    {
        if (taskIds.Count == 0)
        {
            return [];
        }

        const string sql = """
            SELECT TaskId, Label
            FROM dbo.TaskLabels
            WHERE TaskId IN @TaskIds
            ORDER BY Label;
            """;

        var rows = await connection.QueryAsync<TaskLabelRecord>(new CommandDefinition(
            sql,
            new { TaskIds = taskIds.ToArray() },
            cancellationToken: cancellationToken));

        return rows
            .GroupBy(static row => row.TaskId)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static row => row.Label).ToList());
    }

    private static async Task ReplaceLabelsAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        Guid taskId,
        IReadOnlyCollection<string> labels,
        CancellationToken cancellationToken)
    {
        const string deleteSql = "DELETE FROM dbo.TaskLabels WHERE TaskId = @TaskId;";
        await connection.ExecuteAsync(new CommandDefinition(
            deleteSql,
            new { TaskId = taskId },
            transaction: transaction,
            cancellationToken: cancellationToken));

        if (labels.Count == 0)
        {
            return;
        }

        const string insertSql = """
            INSERT INTO dbo.TaskLabels (TaskId, Label)
            VALUES (@TaskId, @Label);
            """;

        foreach (var label in labels)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                insertSql,
                new { TaskId = taskId, Label = label },
                transaction: transaction,
                cancellationToken: cancellationToken));
        }
    }

    private sealed class TaskRecord
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string Priority { get; init; } = string.Empty;
        public DateTime? TargetStartDate { get; init; }
        public DateTime? TargetDueDate { get; init; }
        public DateTime? DueDate { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    private sealed class TaskLabelRecord
    {
        public Guid TaskId { get; init; }
        public string Label { get; init; } = string.Empty;
    }

    private sealed class TaskCommentRecord
    {
        public Guid Id { get; init; }
        public Guid TaskId { get; init; }
        public string Content { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
    }
}
