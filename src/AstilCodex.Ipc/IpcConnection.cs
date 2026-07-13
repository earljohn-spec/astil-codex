using AstilCodex.Contracts;

namespace AstilCodex.Ipc;

public sealed class IpcConnection : IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _disposed;

    public IpcConnection(Stream stream, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead || !stream.CanWrite)
        {
            throw new ArgumentException("IPC stream must be readable and writable.", nameof(stream));
        }

        _stream = stream;
        _leaveOpen = leaveOpen;
    }

    public async ValueTask SendAsync(
        IpcEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await IpcFrameCodec.WriteAsync(_stream, envelope, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public ValueTask<IpcEnvelope?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return IpcFrameCodec.ReadAsync(_stream, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _writeLock.Dispose();
        if (!_leaveOpen)
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
        }
    }
}
