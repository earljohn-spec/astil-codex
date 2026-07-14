namespace AstilCodex.Providers.Security;

public interface ISecretStore
{
    ValueTask SetAsync(
        string secretId,
        string secret,
        CancellationToken cancellationToken = default);

    ValueTask<string?> GetAsync(
        string secretId,
        CancellationToken cancellationToken = default);

    ValueTask<bool> DeleteAsync(
        string secretId,
        CancellationToken cancellationToken = default);
}
