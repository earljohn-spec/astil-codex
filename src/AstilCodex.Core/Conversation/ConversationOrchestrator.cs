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
        Func<AvatarStateEvent, CancellationToken, ValueTask>? stateChanged = null,
        Func<string, CancellationToken, ValueTask>? chunkReceived = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stopwatch = Stopwatch.StartNew();
        var task = _classifier.Classify(request.UserText, request.Mode);
        var manifest = _taskRouter.Route(task, policy, hardware);

        if (manifest.ReasoningLocation == ReasoningLocation.Unavailable)
        {
            await ReportStateAsync(
                stateChanged,
                AvatarStateEvent.Now(AvatarState.Error, manifest.Explanation),
                cancellationToken).ConfigureAwait(false);
            return new ChatResult(manifest.Explanation, manifest, stopwatch.Elapsed);
        }

        if (manifest.ReasoningLocation == ReasoningLocation.Ask)
        {
            const string approvalMessage =
                "Provider selection requires your approval before this request can continue.";
            await ReportStateAsync(
                stateChanged,
                AvatarStateEvent.Now(AvatarState.Ready, approvalMessage),
                cancellationToken).ConfigureAwait(false);
            return new ChatResult(approvalMessage, manifest, stopwatch.Elapsed);
        }

        await ReportStateAsync(
            stateChanged,
            AvatarStateEvent.Now(AvatarState.Thinking, "Preparing response"),
            cancellationToken).ConfigureAwait(false);
        var buffer = new StringBuilder();

        try
        {
            await ReportStateAsync(
                stateChanged,
                AvatarStateEvent.Now(AvatarState.Speaking, "Streaming response"),
                cancellationToken).ConfigureAwait(false);
            await foreach (var chunk in _chatProvider.StreamReplyAsync(request, cancellationToken))
            {
                buffer.Append(chunk);
                if (chunkReceived is not null)
                {
                    await chunkReceived(chunk, cancellationToken).ConfigureAwait(false);
                }
            }

            await ReportStateAsync(
                stateChanged,
                AvatarStateEvent.Now(AvatarState.Ready, "Response complete"),
                cancellationToken).ConfigureAwait(false);
            return new ChatResult(buffer.ToString(), manifest, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await ReportStateWithoutCancellationAsync(
                stateChanged,
                AvatarStateEvent.Now(AvatarState.Cancelled, "Request cancelled"))
                .ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await ReportStateWithoutCancellationAsync(
                stateChanged,
                AvatarStateEvent.Now(AvatarState.Error, exception.Message))
                .ConfigureAwait(false);
            throw;
        }
    }

    private static ValueTask ReportStateAsync(
        Func<AvatarStateEvent, CancellationToken, ValueTask>? callback,
        AvatarStateEvent state,
        CancellationToken cancellationToken) =>
        callback is null ? ValueTask.CompletedTask : callback(state, cancellationToken);

    private static async ValueTask ReportStateWithoutCancellationAsync(
        Func<AvatarStateEvent, CancellationToken, ValueTask>? callback,
        AvatarStateEvent state)
    {
        if (callback is null)
        {
            return;
        }

        try
        {
            await callback(state, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // The original failure or cancellation remains authoritative.
        }
    }
}
