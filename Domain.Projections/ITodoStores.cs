namespace Domain.Projections;

/// <summary>
/// Write-Zugriffsmuster für Todo-ReadModels.
/// Wird vom Writer (TodoProjection) verwendet.
/// </summary>
public interface ITodoWriteStore
{
    Task UpsertAsync(TodoReadModel model);
    Task UpdateStatusAsync(Guid id, Todo.TodoStatus status, DateTimeOffset? erledigtAm);
    Task UpdateTitelAsync(Guid id, string neuerTitel);
    Task UpdatePrioritaetAsync(Guid id, Todo.Prioritaet neuePrioritaet);
    Task AddTagAsync(Guid id, string tag);
    Task RemoveTagAsync(Guid id, string tag);
    Task<TodoReadModel?> FindByIdAsync(Guid id);
}

/// <summary>
/// Read-Zugriffsmuster für Todo-ReadModels.
/// Wird vom Reader (TodoReader) verwendet.
/// </summary>
public interface ITodoReadStore
{
    Task<TodoReadModel?> FindByIdAsync(Guid id);
    Task<IReadOnlyList<TodoReadModel>> GetAllAsync();
}