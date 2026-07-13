using System.IO.Pipes;

namespace AstilCodex.Ipc;

public static class NamedPipeIpcClient
{
    public static async Task<IpcConnection> ConnectAsync(
        string pipeName,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        var client = new NamedPipeClientStream(
            serverName: ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            await client.ConnectAsync(timeoutSource.Token).ConfigureAwait(false);
            return new IpcConnection(client);
        }
        catch
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
