using System.Collections.Concurrent;
using System.Text.Json;
using AstilCodex.Contracts;
using AstilCodex.Core.Conversation;
using AstilCodex.Ipc;
using AstilCodex.Memory;

namespace AstilCodex.Core.Host;

public sealed class CoreIpcService(
    ConversationOrchestrator orchestrator,
    IConversationStore conversationStore,
    HardwareProfile hardware)
{
    private readonly ConversationOrchestrator _orchestrator =
        orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    private readonly IConversationStore _conversationStore =
        conversationStore ?? throw new ArgumentNullException(nameof(conversationStore));
    private readonly HardwareProfile _hardware =
        hardware ?? throw new ArgumentNullException(nameof(hardware));
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeCancellation = new();
    private readonly ConcurrentDictionary<string, Task> _activeTasks = new();

    public async Task RunClientAsync(
        IpcConnection connection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var envelope = await connection.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                if (envelope is null)
                {
                    break;
                }

                try
                {
                    await HandleEnvelopeAsync(connection, envelope, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception exception) when (
                    exception is JsonException or InvalidDataException or ArgumentException)
                {
                    await SendErrorAsync(
                        connection,
                        envelope.MessageId,
                        "invalid_message",
                        exception.Message,
                        cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            foreach (var source in _activeCancellation.Values)
            {
                source.Cancel();
            }

            var active = _activeTasks.Values.ToArray();
            if (active.Length > 0)
            {
                try
                {
                    await Task.WhenAll(active).ConfigureAwait(false);
                }
                catch
                {
                    // Individual task failures are converted into IPC error events.
                }
            }
        }
    }

    private async Task HandleEnvelopeAsync(
        IpcConnection connection,
        IpcEnvelope envelope,
        CancellationToken clientCancellationToken)
    {
        if (!string.Equals(envelope.ContractVersion, Protocol.Version, StringComparison.Ordinal))
        {
            await SendErrorAsync(
                connection,
                envelope.MessageId,
                "contract_version_mismatch",
                $"Expected contract version {Protocol.Version}; received {envelope.ContractVersion}.",
                clientCancellationToken).ConfigureAwait(false);
            return;
        }

        switch (envelope.MessageType)
        {
            case IpcMessageTypes.HealthRequest:
                _ = IpcSerializer.GetPayload<HealthRequest>(envelope);
                await connection.SendAsync(
                    IpcSerializer.CreateEnvelope(
                        IpcMessageTypes.HealthResponse,
                        new HealthResponse(
                            "Astil Codex Core",
                            Protocol.Version,
                            "ready",
                            DateTimeOffset.UtcNow),
                        envelope.MessageId),
                    clientCancellationToken).ConfigureAwait(false);
                break;

            case IpcMessageTypes.ChatRequest:
                StartChat(connection, envelope, clientCancellationToken);
                break;

            case IpcMessageTypes.CancelRequest:
                await CancelTaskAsync(connection, envelope, clientCancellationToken)
                    .ConfigureAwait(false);
                break;

            default:
                await SendErrorAsync(
                    connection,
                    envelope.MessageId,
                    "unknown_message_type",
                    $"Unsupported message type: {envelope.MessageType}",
                    clientCancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private void StartChat(
        IpcConnection connection,
        IpcEnvelope envelope,
        CancellationToken clientCancellationToken)
    {
        var requestId = envelope.MessageId;
        var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(clientCancellationToken);
        if (!_activeCancellation.TryAdd(requestId, linkedSource))
        {
            linkedSource.Dispose();
            _ = SendErrorAsync(
                connection,
                requestId,
                "duplicate_request",
                "A task with the same request ID is already active.",
                clientCancellationToken);
            return;
        }

        var task = ProcessChatAsync(connection, envelope, linkedSource.Token);
        _activeTasks[requestId] = task;
        _ = CleanupWhenCompleteAsync(requestId, task);
    }

    private async Task CleanupWhenCompleteAsync(string requestId, Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // ProcessChatAsync normally converts failures into IPC error events.
        }

        _activeTasks.TryRemove(requestId, out _);
        if (_activeCancellation.TryRemove(requestId, out var source))
        {
            source.Dispose();
        }
    }

    private async Task ProcessChatAsync(
        IpcConnection connection,
        IpcEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var requestId = envelope.MessageId;
        try
        {
            var command = IpcSerializer.GetPayload<ChatIpcRequest>(envelope);
            ValidateChatRequest(command);
            await _conversationStore.UpsertSessionAsync(
                command.SessionId,
                command.Mode,
                cancellationToken).ConfigureAwait(false);

            var storedHistory = await _conversationStore.GetMessagesAsync(
                command.SessionId,
                limit: 200,
                cancellationToken).ConfigureAwait(false);
            var history = storedHistory
                .Select(message => new ChatMessage(
                    message.Role,
                    message.Content,
                    message.CreatedAt))
                .ToArray();

            await _conversationStore.AddMessageAsync(
                command.SessionId,
                "user",
                command.UserText,
                cancellationToken).ConfigureAwait(false);

            await connection.SendAsync(
                IpcSerializer.CreateEnvelope(
                    IpcMessageTypes.ChatStarted,
                    new ChatStartedEvent(requestId, command.SessionId),
                    requestId),
                cancellationToken).ConfigureAwait(false);

            var chatRequest = new ChatRequest(
                command.SessionId,
                command.Mode,
                command.UserText,
                history);

            var result = await _orchestrator.ReplyAsync(
                chatRequest,
                command.Policy,
                _hardware,
                stateChanged: (state, token) => connection.SendAsync(
                    IpcSerializer.CreateEnvelope(
                        IpcMessageTypes.AvatarState,
                        state,
                        requestId),
                    token),
                chunkReceived: (chunk, token) => connection.SendAsync(
                    IpcSerializer.CreateEnvelope(
                        IpcMessageTypes.ChatChunk,
                        new ChatChunkEvent(requestId, chunk),
                        requestId),
                    token),
                cancellationToken).ConfigureAwait(false);

            await _conversationStore.AddMessageAsync(
                command.SessionId,
                "assistant",
                result.Text,
                cancellationToken).ConfigureAwait(false);

            await connection.SendAsync(
                IpcSerializer.CreateEnvelope(
                    IpcMessageTypes.ChatCompleted,
                    new ChatCompletedEvent(
                        requestId,
                        result.Text,
                        result.Manifest,
                        result.Duration.TotalMilliseconds),
                    requestId),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await TrySendAsync(
                connection,
                IpcSerializer.CreateEnvelope(
                    IpcMessageTypes.TaskCancelled,
                    new TaskCancelledEvent(requestId),
                    requestId)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await TrySendAsync(
                connection,
                IpcSerializer.CreateEnvelope(
                    IpcMessageTypes.Error,
                    new ErrorEvent("chat_failed", exception.Message, requestId),
                    requestId)).ConfigureAwait(false);
        }
    }

    private async Task CancelTaskAsync(
        IpcConnection connection,
        IpcEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var request = IpcSerializer.GetPayload<CancelTaskRequest>(envelope);
        if (_activeCancellation.TryGetValue(request.RequestId, out var source))
        {
            source.Cancel();
            return;
        }

        await SendErrorAsync(
            connection,
            envelope.MessageId,
            "task_not_active",
            $"Task '{request.RequestId}' is not active.",
            cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateChatRequest(ChatIpcRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserText);
        if (request.SessionId.Length > 128)
        {
            throw new InvalidDataException("Session ID cannot exceed 128 characters.");
        }

        if (request.UserText.Length > 64 * 1024)
        {
            throw new InvalidDataException("Chat request cannot exceed 64 KiB of text.");
        }
    }

    private static ValueTask SendErrorAsync(
        IpcConnection connection,
        string correlationId,
        string code,
        string message,
        CancellationToken cancellationToken) =>
        connection.SendAsync(
            IpcSerializer.CreateEnvelope(
                IpcMessageTypes.Error,
                new ErrorEvent(code, message, correlationId),
                correlationId),
            cancellationToken);

    private static async Task TrySendAsync(IpcConnection connection, IpcEnvelope envelope)
    {
        try
        {
            await connection.SendAsync(envelope, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // The client disconnected; no additional recovery is possible for this response.
        }
    }
}
