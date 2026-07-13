using AstilCodex.Contracts;

namespace AstilCodex.Memory;

public interface IConversationStore
{
    string DatabasePath { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task UpsertSessionAsync(
        string sessionId,
        AssistantMode mode,
        CancellationToken cancellationToken = default);

    Task<long> AddMessageAsync(
        string sessionId,
        string role,
        string content,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredChatMessage>> GetMessagesAsync(
        string sessionId,
        int limit = 200,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    Task<int> PruneSessionsOlderThanAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default);

    Task ClearAllAsync(CancellationToken cancellationToken = default);
}
