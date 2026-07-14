using System.Text.Json;

namespace AstilCodex.Contracts;

public static class IpcMessageTypes
{
    public const string HealthRequest = "health.request";
    public const string HealthResponse = "health.response";
    public const string ChatRequest = "chat.request";
    public const string ChatStarted = "chat.started";
    public const string ChatChunk = "chat.chunk";
    public const string ChatCompleted = "chat.completed";
    public const string AvatarState = "avatar.state";
    public const string CancelRequest = "task.cancel";
    public const string TaskCancelled = "task.cancelled";
    public const string Error = "error";
}

public sealed record IpcEnvelope(
    string ContractVersion,
    string MessageId,
    string? CorrelationId,
    string MessageType,
    DateTimeOffset SentAt,
    JsonElement Payload);

public sealed record HealthRequest(string ClientName);

public sealed record HealthResponse(
    string ServiceName,
    string ServiceVersion,
    string Status,
    DateTimeOffset ServerTime);

public sealed record ChatIpcRequest(
    string SessionId,
    AssistantMode Mode,
    string UserText,
    ProcessingPolicy Policy);

public sealed record ChatStartedEvent(string RequestId, string SessionId);

public sealed record ChatChunkEvent(string RequestId, string Text);

public sealed record ChatCompletedEvent(
    string RequestId,
    string Text,
    TaskManifest Manifest,
    double DurationMilliseconds,
    string ProviderId);

public sealed record CancelTaskRequest(string RequestId);

public sealed record TaskCancelledEvent(string RequestId);

public sealed record ErrorEvent(
    string Code,
    string Message,
    string? RequestId = null);
