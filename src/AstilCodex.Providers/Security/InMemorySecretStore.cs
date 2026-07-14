using System.Collections.Concurrent;

namespace AstilCodex.Providers.Security;

public sealed class InMemorySecretStore : ISecretStore
{
    private readonly ConcurrentDictionary<string, string> _secrets =
        new(StringComparer.Ordinal);

    public ValueTask SetAsync(
        string secretId,
        string secret,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(secretId);
        ArgumentNullException.ThrowIfNull(secret);
        _secrets[secretId] = secret;
        return ValueTask.CompletedTask;
    }

    public ValueTask<string?> GetAsync(
        string secretId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(secretId);
        _secrets.TryGetValue(secretId, out var secret);
        return ValueTask.FromResult(secret);
    }

    public ValueTask<bool> DeleteAsync(
        string secretId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(secretId);
        return ValueTask.FromResult(_secrets.TryRemove(secretId, out _));
    }
}
