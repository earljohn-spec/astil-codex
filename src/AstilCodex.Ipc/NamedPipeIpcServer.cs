using System.IO.Pipes;

namespace AstilCodex.Ipc;

public sealed class NamedPipeIpcServer
{
    private readonly TaskCompletionSource<bool> _ready =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public NamedPipeIpcServer(string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        PipeName = pipeName;
    }

    public string PipeName { get; }

    public Task Ready => _ready.Task;

    public async Task RunSingleClientAsync(
        Func<IpcConnection, CancellationToken, Task> clientHandler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clientHandler);
        var options = PipeOptions.Asynchronous;
        if (OperatingSystem.IsWindows())
        {
            options |= PipeOptions.CurrentUserOnly;
        }

        await using var pipe = new NamedPipeServerStream(
            PipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            options);
        _ready.TrySetResult(true);
        await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = new IpcConnection(pipe, leaveOpen: true);
        await clientHandler(connection, cancellationToken).ConfigureAwait(false);
    }
}
