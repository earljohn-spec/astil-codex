using System.Text.Json;
using System.Text.Json.Serialization;
using AstilCodex.Contracts;

namespace AstilCodex.Ipc;

public static class IpcSerializer
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static IpcEnvelope CreateEnvelope<TPayload>(
        string messageType,
        TPayload payload,
        string? correlationId = null,
        string? messageId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        ArgumentNullException.ThrowIfNull(payload);
        return new IpcEnvelope(
            Protocol.Version,
            messageId ?? Guid.NewGuid().ToString("N"),
            correlationId,
            messageType,
            DateTimeOffset.UtcNow,
            JsonSerializer.SerializeToElement(payload, Options));
    }

    public static TPayload GetPayload<TPayload>(IpcEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return envelope.Payload.Deserialize<TPayload>(Options)
            ?? throw new InvalidDataException(
                $"Message '{envelope.MessageType}' contains an empty or invalid payload.");
    }

    public static byte[] Serialize(IpcEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return JsonSerializer.SerializeToUtf8Bytes(envelope, Options);
    }

    public static IpcEnvelope Deserialize(ReadOnlySpan<byte> data) =>
        JsonSerializer.Deserialize<IpcEnvelope>(data, Options)
        ?? throw new InvalidDataException("IPC frame did not contain a valid envelope.");

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = false
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
