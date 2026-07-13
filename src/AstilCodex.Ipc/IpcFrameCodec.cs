using System.Buffers.Binary;
using AstilCodex.Contracts;

namespace AstilCodex.Ipc;

public static class IpcFrameCodec
{
    public const int MaximumPayloadBytes = 4 * 1024 * 1024;
    private const int HeaderBytes = sizeof(int);

    public static async ValueTask WriteAsync(
        Stream stream,
        IpcEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var payload = IpcSerializer.Serialize(envelope);
        if (payload.Length > MaximumPayloadBytes)
        {
            throw new InvalidDataException(
                $"IPC payload is {payload.Length} bytes; maximum is {MaximumPayloadBytes}.");
        }

        var header = new byte[HeaderBytes];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<IpcEnvelope?> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var header = new byte[HeaderBytes];
        var headerRead = await ReadExactlyOrEndAsync(stream, header, cancellationToken)
            .ConfigureAwait(false);
        if (!headerRead)
        {
            return null;
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is <= 0 or > MaximumPayloadBytes)
        {
            throw new InvalidDataException($"Invalid IPC payload length: {length}.");
        }

        var payload = new byte[length];
        if (!await ReadExactlyOrEndAsync(stream, payload, cancellationToken).ConfigureAwait(false))
        {
            throw new EndOfStreamException("IPC stream ended in the middle of a frame.");
        }

        return IpcSerializer.Deserialize(payload);
    }

    private static async ValueTask<bool> ReadExactlyOrEndAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                if (offset == 0)
                {
                    return false;
                }

                throw new EndOfStreamException("IPC stream ended before the frame was complete.");
            }

            offset += read;
        }

        return true;
    }
}
