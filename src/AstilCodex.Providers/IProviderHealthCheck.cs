namespace AstilCodex.Providers;

public interface IProviderHealthCheck
{
    Task<ProviderHealthResult> CheckHealthAsync(
        CancellationToken cancellationToken = default);
}

public sealed record ProviderHealthResult(
    bool IsHealthy,
    string Status,
    bool ConfiguredModelFound,
    IReadOnlyList<string> AvailableModels);
