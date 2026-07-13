using AstilCodex.Contracts;

namespace AstilCodex.Core.Providers;

public interface IChatProvider
{
    string ProviderId { get; }

    IAsyncEnumerable<string> StreamReplyAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default);
}
