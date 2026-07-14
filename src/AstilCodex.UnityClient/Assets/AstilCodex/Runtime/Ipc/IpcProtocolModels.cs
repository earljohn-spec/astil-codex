using System;

namespace AstilCodex.UnityClient.Ipc
{
    internal static class IpcProtocol
    {
        public const string Version = "1.0";
        public const string PipeName = "astil-codex-core-v1";
        public const int MaximumPayloadBytes = 4 * 1024 * 1024;

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

    [Serializable]
    internal class EnvelopeHeader
    {
        public string contractVersion = string.Empty;
        public string messageId = string.Empty;
        public string correlationId = string.Empty;
        public string messageType = string.Empty;
        public string sentAt = string.Empty;
    }

    [Serializable]
    internal sealed class HealthRequestEnvelope : EnvelopeHeader
    {
        public HealthRequestPayload payload = new HealthRequestPayload();
    }

    [Serializable]
    internal sealed class HealthRequestPayload
    {
        public string clientName = "AstilCodex.UnityClient";
    }

    [Serializable]
    internal sealed class HealthResponseEnvelope : EnvelopeHeader
    {
        public HealthResponsePayload payload = new HealthResponsePayload();
    }

    [Serializable]
    internal sealed class HealthResponsePayload
    {
        public string serviceName = string.Empty;
        public string serviceVersion = string.Empty;
        public string status = string.Empty;
        public string serverTime = string.Empty;
    }

    [Serializable]
    internal sealed class ChatRequestEnvelope : EnvelopeHeader
    {
        public ChatRequestPayload payload = new ChatRequestPayload();
    }

    [Serializable]
    internal sealed class ChatRequestPayload
    {
        public string sessionId = string.Empty;
        public string mode = "companion";
        public string userText = string.Empty;
        public string policy = "autoPrivacyFirst";
    }

    [Serializable]
    internal sealed class ChatStartedEnvelope : EnvelopeHeader
    {
        public ChatStartedPayload payload = new ChatStartedPayload();
    }

    [Serializable]
    internal sealed class ChatStartedPayload
    {
        public string requestId = string.Empty;
        public string sessionId = string.Empty;
    }

    [Serializable]
    internal sealed class ChatChunkEnvelope : EnvelopeHeader
    {
        public ChatChunkPayload payload = new ChatChunkPayload();
    }

    [Serializable]
    internal sealed class ChatChunkPayload
    {
        public string requestId = string.Empty;
        public string text = string.Empty;
    }

    [Serializable]
    internal sealed class ChatCompletedEnvelope : EnvelopeHeader
    {
        public ChatCompletedPayload payload = new ChatCompletedPayload();
    }

    [Serializable]
    internal sealed class ChatCompletedPayload
    {
        public string requestId = string.Empty;
        public string text = string.Empty;
        public float durationMilliseconds;
        public string providerId = string.Empty;
    }

    [Serializable]
    internal sealed class AvatarStateEnvelope : EnvelopeHeader
    {
        public AvatarStatePayload payload = new AvatarStatePayload();
    }

    [Serializable]
    internal sealed class AvatarStatePayload
    {
        public string state = "ready";
        public string detail = string.Empty;
        public string timestamp = string.Empty;
    }

    [Serializable]
    internal sealed class CancelRequestEnvelope : EnvelopeHeader
    {
        public CancelRequestPayload payload = new CancelRequestPayload();
    }

    [Serializable]
    internal sealed class CancelRequestPayload
    {
        public string requestId = string.Empty;
    }

    [Serializable]
    internal sealed class TaskCancelledEnvelope : EnvelopeHeader
    {
        public TaskCancelledPayload payload = new TaskCancelledPayload();
    }

    [Serializable]
    internal sealed class TaskCancelledPayload
    {
        public string requestId = string.Empty;
    }

    [Serializable]
    internal sealed class ErrorEnvelope : EnvelopeHeader
    {
        public ErrorPayload payload = new ErrorPayload();
    }

    [Serializable]
    internal sealed class ErrorPayload
    {
        public string code = string.Empty;
        public string message = string.Empty;
        public string requestId = string.Empty;
    }
}
