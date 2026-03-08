using Dapper;
using TaskTracker.Application.Abstractions;
using TaskTracker.Domain.Entities;
using DomainTaskPriority = TaskTracker.Domain.Enums.TaskPriority;
using DomainTaskStatus = TaskTracker.Domain.Enums.TaskStatus;

namespace TaskTracker.Infrastructure.Persistence;

public sealed class TaskRepository(ISqlConnectionFactory connectionFactory) : ITaskRepository
{
    /// <summary>
    /// Recupera todas las tareas persistidas junto con sus etiquetas asociadas.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>La colección completa de tareas almacenadas.</returns>
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

    /// <summary>
    /// Recupera una tarea por identificador junto con sus etiquetas.
    /// </summary>
    /// <param name="id">Identificador de la tarea a buscar.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>La tarea encontrada o <see langword="null"/> cuando no existe.</returns>
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

    /// <summary>
    /// Inserta una nueva tarea en la base de datos y persiste sus etiquetas en la misma transacción.
    /// </summary>
    /// <param name="task">Entidad de tarea a crear.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>El identificador de la tarea creada.</returns>
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

    /// <summary>
    /// Actualiza los datos de una tarea y reemplaza sus etiquetas persistidas.
    /// </summary>
    /// <param name="task">Entidad de tarea con el nuevo estado de datos.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns><see langword="true"/> si la tarea fue actualizada; en caso contrario, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Elimina una tarea de la base de datos por su identificador.
    /// </summary>
    /// <param name="id">Identificador de la tarea a eliminar.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns><see langword="true"/> si se eliminó al menos un registro; en caso contrario, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Actualiza el estado persistido de una tarea.
    /// </summary>
    /// <param name="id">Identificador de la tarea a modificar.</param>
    /// <param name="status">Nuevo estado a guardar.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns><see langword="true"/> si el estado fue actualizado; en caso contrario, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Recupera los comentarios almacenados para una tarea.
    /// </summary>
    /// <param name="taskId">Identificador de la tarea consultada.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>La colección de comentarios ordenada por fecha de creación ascendente.</returns>
    public async Task<IReadOnlyCollection<TaskComment>> GetCommentsAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, TaskId, Content, ImageDataUrl, ImageFileName, CreatedAt
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
            ImageDataUrl = row.ImageDataUrl,
            ImageFileName = row.ImageFileName,
            CreatedAt = row.CreatedAt
        }).ToArray();
    }

    /// <summary>
    /// Inserta un comentario en la base de datos cuando la tarea relacionada existe.
    /// </summary>
    /// <param name="comment">Comentario que se desea persistir.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>El identificador creado o <see langword="null"/> si la tarea no existe.</returns>
    public async Task<Guid?> AddCommentAsync(TaskComment comment, CancellationToken cancellationToken = default)
    {
        const string taskExistsSql = "SELECT 1 FROM dbo.Tasks WHERE Id = @TaskId;";
        const string insertSql = """
            INSERT INTO dbo.TaskComments (Id, TaskId, Content, ImageDataUrl, ImageFileName, CreatedAt)
            VALUES (@Id, @TaskId, @Content, @ImageDataUrl, @ImageFileName, @CreatedAt);
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
                comment.ImageDataUrl,
                comment.ImageFileName,
                comment.CreatedAt
            },
            cancellationToken: cancellationToken));

        return comment.Id;
    }

    /// <summary>
    /// Recupera la actividad histórica de una tarea.
    /// </summary>
    /// <param name="taskId">Identificador de la tarea consultada.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>La colección de eventos de actividad ordenada del más reciente al más antiguo.</returns>
    public async Task<IReadOnlyCollection<TaskActivity>> GetActivityAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, TaskId, Action, Detail, CreatedAt
            FROM dbo.TaskActivity
            WHERE TaskId = @TaskId
            ORDER BY CreatedAt DESC;
            """;

        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<TaskActivityRecord>(
            new CommandDefinition(sql, new { TaskId = taskId }, cancellationToken: cancellationToken));

        return rows.Select(static row => new TaskActivity
        {
            Id = row.Id,
            TaskId = row.TaskId,
            Action = row.Action,
            Detail = row.Detail,
            CreatedAt = row.CreatedAt
        }).ToArray();
    }

    /// <summary>
    /// Recupera el feed de actividad reciente desde una fecha determinada.
    /// </summary>
    /// <param name="fromUtc">Fecha mínima en UTC para devolver actividad.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>La colección de eventos recientes ordenada del más reciente al más antiguo.</returns>
    public async Task<IReadOnlyCollection<TaskActivity>> GetRecentActivityFeedAsync(DateTime fromUtc, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, TaskId, Action, Detail, CreatedAt
            FROM dbo.TaskActivity
            WHERE CreatedAt >= @FromUtc
            ORDER BY CreatedAt DESC;
            """;

        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<TaskActivityRecord>(
            new CommandDefinition(sql, new { FromUtc = fromUtc }, cancellationToken: cancellationToken));

        return rows.Select(static row => new TaskActivity
        {
            Id = row.Id,
            TaskId = row.TaskId,
            Action = row.Action,
            Detail = row.Detail,
            CreatedAt = row.CreatedAt
        }).ToArray();
    }

    /// <summary>
    /// Inserta un evento de actividad en la base de datos.
    /// </summary>
    /// <param name="activity">Evento de actividad a persistir.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>Una tarea completada cuando la inserción finaliza.</returns>
    public async Task AddActivityAsync(TaskActivity activity, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO dbo.TaskActivity (Id, TaskId, Action, Detail, CreatedAt)
            VALUES (@Id, @TaskId, @Action, @Detail, @CreatedAt);
            """;

        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                activity.Id,
                activity.TaskId,
                activity.Action,
                activity.Detail,
                activity.CreatedAt
            },
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Mapea un registro de base de datos a la entidad de dominio de tarea.
    /// </summary>
    /// <param name="record">Registro recuperado desde la base de datos.</param>
    /// <param name="labels">Etiquetas asociadas a la tarea.</param>
    /// <returns>La entidad de dominio construida a partir del registro recibido.</returns>
    private static TaskItem Map(TaskRecord record, List<string> labels)
    {
        if (!Enum.TryParse<DomainTaskStatus>(record.Status, ignoreCase: true, out var status))
        {
            status = DomainTaskStatus.Todo;
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

    /// <summary>
    /// Carga las etiquetas agrupadas por identificador de tarea para un conjunto de tareas.
    /// </summary>
    /// <param name="connection">Conexión de base de datos ya abierta o reutilizable.</param>
    /// <param name="taskIds">Identificadores de tareas cuyas etiquetas se consultarán.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>Un diccionario cuya clave es el identificador de tarea y cuyo valor es su lista de etiquetas.</returns>
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

    /// <summary>
    /// Reemplaza la colección completa de etiquetas de una tarea dentro de la transacción actual.
    /// </summary>
    /// <param name="connection">Conexión de base de datos utilizada para ejecutar los comandos.</param>
    /// <param name="transaction">Transacción activa en la que se reemplazarán las etiquetas.</param>
    /// <param name="taskId">Identificador de la tarea cuyas etiquetas se reemplazarán.</param>
    /// <param name="labels">Nueva colección de etiquetas que debe quedar persistida.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>Una tarea completada cuando la operación finaliza.</returns>
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
        public string? ImageDataUrl { get; init; }
        public string? ImageFileName { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    private sealed class TaskActivityRecord
    {
        public Guid Id { get; init; }
        public Guid TaskId { get; init; }
        public string Action { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
    }
}
