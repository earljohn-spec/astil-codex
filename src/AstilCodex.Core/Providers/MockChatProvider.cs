using System.Runtime.CompilerServices;
using AstilCodex.Contracts;

namespace AstilCodex.Core.Providers;

public sealed class MockChatProvider(TimeSpan? chunkDelay = null) : IChatProvider
{
    private readonly TimeSpan _chunkDelay = chunkDelay ?? TimeSpan.FromMilliseconds(22);

    public string ProviderId => "mock.local";

    public async IAsyncEnumerable<string> StreamReplyAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var response = SelectResponse(request);
        var words = response.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (var index = 0; index < words.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_chunkDelay > TimeSpan.Zero)
            {
                await Task.Delay(_chunkDelay, cancellationToken).ConfigureAwait(false);
            }

            yield return index == 0 ? words[index] : " " + words[index];
        }
    }

    private static string SelectResponse(ChatRequest request) => request.Mode switch
    {
        AssistantMode.Companion =>
            "I am here. This response comes from the local mock provider, so no cloud service or computer tool was used.",
        AssistantMode.Assistant =>
            "I can turn that into a clear plan. Personal context remains local, and external actions will require approval.",
        AssistantMode.Focus =>
            "Focus mode is active. I will keep the response concise and avoid unnecessary interruptions.",
        AssistantMode.Developer =>
            "I prepared a developer task manifest. Code changes and terminal commands require an approved workspace and permission.",
        AssistantMode.Creator =>
            "I prepared a creator task manifest. Blender execution and file output require explicit local approval.",
        _ => "The mock provider received your request."
    };
}
