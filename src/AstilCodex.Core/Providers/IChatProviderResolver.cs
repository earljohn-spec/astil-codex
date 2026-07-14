using AstilCodex.Contracts;

namespace AstilCodex.Core.Providers;

public interface IChatProviderResolver
{
    ValueTask<IChatProvider> ResolveAsync(
        ReasoningLocation reasoningLocation,
        CancellationToken cancellationToken = default);
}
