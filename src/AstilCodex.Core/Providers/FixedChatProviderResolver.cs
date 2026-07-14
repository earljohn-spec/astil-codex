using AstilCodex.Contracts;

namespace AstilCodex.Core.Providers;

public sealed class FixedChatProviderResolver(IChatProvider provider) : IChatProviderResolver
{
    private readonly IChatProvider _provider =
        provider ?? throw new ArgumentNullException(nameof(provider));

    public ValueTask<IChatProvider> ResolveAsync(
        ReasoningLocation reasoningLocation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_provider);
    }
}
