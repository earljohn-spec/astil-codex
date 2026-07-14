using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace AstilCodex.Providers.Security;

[SupportedOSPlatform("windows")]
public sealed class DpapiFileSecretStore : ISecretStore
{
    private static readonly byte[] EntropyPrefix = Encoding.UTF8.GetBytes("AstilCodex.ProviderSecret.v1:");

    public DpapiFileSecretStore(string secretsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretsDirectory);
        SecretsDirectory = Path.GetFullPath(secretsDirectory);
    }

    public string SecretsDirectory { get; }

    public async ValueTask SetAsync(
        string secretId,
        string secret,
        CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        ValidateSecretId(secretId);
        ArgumentNullException.ThrowIfNull(secret);
        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(SecretsDirectory);
        var clearBytes = Encoding.UTF8.GetBytes(secret);
        try
        {
            var encrypted = ProtectedData.Protect(
                clearBytes,
                GetEntropy(secretId),
                DataProtectionScope.CurrentUser);
            var path = GetSecretPath(secretId);
            var temporaryPath = path + ".tmp";
            await File.WriteAllBytesAsync(temporaryPath, encrypted, cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporaryPath, path, overwrite: true);
            CryptographicOperations.ZeroMemory(encrypted);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clearBytes);
        }
    }

    public async ValueTask<string?> GetAsync(
        string secretId,
        CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        ValidateSecretId(secretId);
        var path = GetSecretPath(secretId);
        if (!File.Exists(path))
        {
            return null;
        }

        var encrypted = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        byte[]? clearBytes = null;
        try
        {
            clearBytes = ProtectedData.Unprotect(
                encrypted,
                GetEntropy(secretId),
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(clearBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encrypted);
            if (clearBytes is not null)
            {
                CryptographicOperations.ZeroMemory(clearBytes);
            }
        }
    }

    public ValueTask<bool> DeleteAsync(
        string secretId,
        CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        ValidateSecretId(secretId);
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetSecretPath(secretId);
        if (!File.Exists(path))
        {
            return ValueTask.FromResult(false);
        }

        File.Delete(path);
        return ValueTask.FromResult(true);
    }

    private string GetSecretPath(string secretId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(secretId));
        return Path.Combine(SecretsDirectory, Convert.ToHexString(hash).ToLowerInvariant() + ".bin");
    }

    private static byte[] GetEntropy(string secretId)
    {
        var idBytes = Encoding.UTF8.GetBytes(secretId);
        var entropy = new byte[EntropyPrefix.Length + idBytes.Length];
        Buffer.BlockCopy(EntropyPrefix, 0, entropy, 0, EntropyPrefix.Length);
        Buffer.BlockCopy(idBytes, 0, entropy, EntropyPrefix.Length, idBytes.Length);
        CryptographicOperations.ZeroMemory(idBytes);
        return entropy;
    }

    private static void ValidateSecretId(string secretId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretId);
        if (secretId.Length > 128)
        {
            throw new ArgumentException("Secret ID cannot exceed 128 characters.", nameof(secretId));
        }
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "DPAPI secret storage is available only on Windows.");
        }
    }
}
