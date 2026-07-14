using System.Diagnostics;
using System.Text;
using AstilCodex.Contracts;
using AstilCodex.Core.Providers;
using AstilCodex.Core.Routing;

namespace AstilCodex.Core.Conversation;

public sealed class ConversationOrchestrator
{
    private readonly IChatProviderResolver _providerResolver;
    private readonly ITaskRouter _taskRouter;
    private readonly TaskClassifier _classifier;

    public ConversationOrchestrator(
        IChatProvider chatProvider,
        ITaskRouter taskRouter,
        TaskClassifier classifier)
        : this(new FixedChatProviderResolver(chatProvider), taskRouter, classifier)
    {
    }

    public ConversationOrchestrator(
        IChatProviderResolver providerResolver,
        ITaskRouter taskRouter,
        TaskClassifier classifier)
    {
        _providerResolver = providerResolver
            ?? throw new ArgumentNullException(nameof(providerResolver));
        _taskRouter = taskRouter ?? throw new ArgumentNullException(nameof(taskRouter));
        _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
    }

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
            return new ChatResult(manifest.Explanation, manifest, stopwatch.Elapsed, "none");
        }

        if (manifest.ReasoningLocation == ReasoningLocation.Ask)
        {
            const string approvalMessage =
                "Provider selection requires your approval before this request can continue.";
            await ReportStateAsync(
                stateChanged,
                AvatarStateEvent.Now(AvatarState.Ready, approvalMessage),
                cancellationToken).ConfigureAwait(false);
            return new ChatResult(approvalMessage, manifest, stopwatch.Elapsed, "none");
        }

        var buffer = new StringBuilder();
        try
        {
            var provider = await _providerResolver.ResolveAsync(
                manifest.ReasoningLocation,
                cancellationToken).ConfigureAwait(false);

            await ReportStateAsync(
                stateChanged,
                AvatarStateEvent.Now(AvatarState.Thinking, $"Preparing response with {provider.ProviderId}"),
                cancellationToken).ConfigureAwait(false);
            await ReportStateAsync(
                stateChanged,
                AvatarStateEvent.Now(AvatarState.Speaking, "Streaming response"),
                cancellationToken).ConfigureAwait(false);

            await foreach (var chunk in provider.StreamReplyAsync(request, cancellationToken))
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
            return new ChatResult(
                buffer.ToString(),
                manifest,
                stopwatch.Elapsed,
                provider.ProviderId);
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
