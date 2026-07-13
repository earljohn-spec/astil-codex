using System.Diagnostics;
using System.Text;
using AstilCodex.Contracts;
using AstilCodex.Core.Providers;
using AstilCodex.Core.Routing;

namespace AstilCodex.Core.Conversation;

public sealed class ConversationOrchestrator(
    IChatProvider chatProvider,
    ITaskRouter taskRouter,
    TaskClassifier classifier)
{
    private readonly IChatProvider _chatProvider =
        chatProvider ?? throw new ArgumentNullException(nameof(chatProvider));
    private readonly ITaskRouter _taskRouter =
        taskRouter ?? throw new ArgumentNullException(nameof(taskRouter));
    private readonly TaskClassifier _classifier =
        classifier ?? throw new ArgumentNullException(nameof(classifier));

    public async Task<ChatResult> ReplyAsync(
        ChatRequest request,
        ProcessingPolicy policy,
        HardwareProfile hardware,
        Action<AvatarStateEvent>? stateChanged = null,
        Action<string>? chunkReceived = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stopwatch = Stopwatch.StartNew();
        var task = _classifier.Classify(request.UserText, request.Mode);
        var manifest = _taskRouter.Route(task, policy, hardware);

        if (manifest.ReasoningLocation == ReasoningLocation.Unavailable)
        {
            stateChanged?.Invoke(AvatarStateEvent.Now(AvatarState.Error, manifest.Explanation));
            return new ChatResult(manifest.Explanation, manifest, stopwatch.Elapsed);
        }

        if (manifest.ReasoningLocation == ReasoningLocation.Ask)
        {
            const string approvalMessage =
                "Provider selection requires your approval before this request can continue.";
            stateChanged?.Invoke(AvatarStateEvent.Now(AvatarState.Ready, approvalMessage));
            return new ChatResult(approvalMessage, manifest, stopwatch.Elapsed);
        }

        stateChanged?.Invoke(AvatarStateEvent.Now(AvatarState.Thinking, "Preparing response"));
        var buffer = new StringBuilder();

        try
        {
            stateChanged?.Invoke(AvatarStateEvent.Now(AvatarState.Speaking, "Streaming response"));
            await foreach (var chunk in _chatProvider.StreamReplyAsync(request, cancellationToken))
            {
                buffer.Append(chunk);
                chunkReceived?.Invoke(chunk);
            }

            stateChanged?.Invoke(AvatarStateEvent.Now(AvatarState.Ready, "Response complete"));
            return new ChatResult(buffer.ToString(), manifest, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stateChanged?.Invoke(AvatarStateEvent.Now(AvatarState.Cancelled, "Request cancelled"));
            throw;
        }
        catch (Exception exception)
        {
            stateChanged?.Invoke(AvatarStateEvent.Now(AvatarState.Error, exception.Message));
            throw;
        }
    }
}
