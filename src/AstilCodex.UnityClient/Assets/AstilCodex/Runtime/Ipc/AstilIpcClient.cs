using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AstilCodex.UnityClient.Ipc
{
    [DisallowMultipleComponent]
    public sealed class AstilIpcClient : MonoBehaviour
    {
        private readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly object _connectionSync = new object();
        private NamedPipeClientStream _pipe;
        private CancellationTokenSource _connectionCancellation;
        private Task _receiveTask;
        private string _currentRequestId = string.Empty;
        private bool _destroyed;

        public event Action<bool, string> ConnectionChanged;
        public event Action<string> HealthChanged;
        public event Action<string> ChatStarted;
        public event Action<string, string> ChatChunkReceived;
        public event Action<string, string, float> ChatCompleted;
        public event Action<string, string> AvatarStateReceived;
        public event Action<string> TaskCancelled;
        public event Action<string, string> ErrorReceived;

        public bool IsConnected
        {
            get
            {
                lock (_connectionSync)
                {
                    return _pipe != null && _pipe.IsConnected;
                }
            }
        }

        public string CurrentRequestId
        {
            get { return _currentRequestId; }
        }

        private void Update()
        {
            var processed = 0;
            while (processed < 128 && _mainThreadActions.TryDequeue(out var action))
            {
                action.Invoke();
                processed++;
            }
        }

        public async Task<bool> ConnectToCoreAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (_destroyed)
            {
                return false;
            }

            if (IsConnected)
            {
                return true;
            }

            QueueMain(() => ConnectionChanged?.Invoke(false, "Connecting"));
            var pipe = new NamedPipeClientStream(
                ".",
                IpcProtocol.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            var timeoutMilliseconds = Mathf.Clamp((int)timeout.TotalMilliseconds, 250, 60000);

            try
            {
                await Task.Run(() => pipe.Connect(timeoutMilliseconds), cancellationToken)
                    .ConfigureAwait(false);
                var connectionCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                lock (_connectionSync)
                {
                    _pipe = pipe;
                    _connectionCancellation = connectionCancellation;
                }

                _receiveTask = ReceiveLoopAsync(pipe, connectionCancellation.Token);
                QueueMain(() => ConnectionChanged?.Invoke(true, "Connected"));
                await SendHealthRequestAsync(connectionCancellation.Token).ConfigureAwait(false);
                return true;
            }
            catch (Exception exception)
            {
                pipe.Dispose();
                QueueMain(() =>
                {
                    ConnectionChanged?.Invoke(false, "Core unavailable");
                    ErrorReceived?.Invoke("connect_failed", exception.Message);
                });
                return false;
            }
        }

        public async Task<string> SendChatAsync(
            string sessionId,
            string mode,
            string userText,
            string policy,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(userText))
            {
                throw new ArgumentException("Message cannot be empty.", nameof(userText));
            }

            var requestId = Guid.NewGuid().ToString("N");
            _currentRequestId = requestId;
            var envelope = new ChatRequestEnvelope
            {
                contractVersion = IpcProtocol.Version,
                messageId = requestId,
                correlationId = string.Empty,
                messageType = IpcProtocol.ChatRequest,
                sentAt = DateTimeOffset.UtcNow.ToString("O"),
                payload = new ChatRequestPayload
                {
                    sessionId = sessionId,
                    mode = mode,
                    userText = userText,
                    policy = policy
                }
            };
            await SendEnvelopeAsync(envelope, cancellationToken).ConfigureAwait(false);
            return requestId;
        }

        public async Task CancelCurrentTaskAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var requestId = _currentRequestId;
            if (string.IsNullOrWhiteSpace(requestId) || !IsConnected)
            {
                return;
            }

            var envelope = new CancelRequestEnvelope
            {
                contractVersion = IpcProtocol.Version,
                messageId = Guid.NewGuid().ToString("N"),
                correlationId = requestId,
                messageType = IpcProtocol.CancelRequest,
                sentAt = DateTimeOffset.UtcNow.ToString("O"),
                payload = new CancelRequestPayload { requestId = requestId }
            };
            await SendEnvelopeAsync(envelope, cancellationToken).ConfigureAwait(false);
        }

        public async Task DisconnectAsync()
        {
            NamedPipeClientStream pipe;
            CancellationTokenSource cancellation;
            Task receiveTask;
            lock (_connectionSync)
            {
                pipe = _pipe;
                cancellation = _connectionCancellation;
                receiveTask = _receiveTask;
                _pipe = null;
                _connectionCancellation = null;
                _receiveTask = null;
            }

            if (pipe == null)
            {
                return;
            }

            cancellation?.Cancel();
            pipe.Dispose();
            if (receiveTask != null)
            {
                try
                {
                    await receiveTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (IOException)
                {
                }
            }

            cancellation?.Dispose();
            QueueMain(() => ConnectionChanged?.Invoke(false, "Disconnected"));
        }

        private async Task SendHealthRequestAsync(CancellationToken cancellationToken)
        {
            var envelope = new HealthRequestEnvelope
            {
                contractVersion = IpcProtocol.Version,
                messageId = Guid.NewGuid().ToString("N"),
                correlationId = string.Empty,
                messageType = IpcProtocol.HealthRequest,
                sentAt = DateTimeOffset.UtcNow.ToString("O"),
                payload = new HealthRequestPayload()
            };
            await SendEnvelopeAsync(envelope, cancellationToken).ConfigureAwait(false);
        }

        private async Task SendEnvelopeAsync(object envelope, CancellationToken cancellationToken)
        {
            NamedPipeClientStream pipe;
            lock (_connectionSync)
            {
                pipe = _pipe;
            }

            if (pipe == null || !pipe.IsConnected)
            {
                throw new IOException("Astil Codex Core is not connected.");
            }

            var json = JsonUtility.ToJson(envelope);
            var payload = Encoding.UTF8.GetBytes(json);
            if (payload.Length > IpcProtocol.MaximumPayloadBytes)
            {
                throw new InvalidDataException("IPC message exceeds the 4 MiB safety limit.");
            }

            var header = BitConverter.GetBytes(payload.Length);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(header);
            }

            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await pipe.WriteAsync(header, 0, header.Length, cancellationToken).ConfigureAwait(false);
                await pipe.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
                await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task ReceiveLoopAsync(
            NamedPipeClientStream pipe,
            CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && pipe.IsConnected)
                {
                    var json = await ReadFrameAsync(pipe, cancellationToken).ConfigureAwait(false);
                    if (json == null)
                    {
                        break;
                    }

                    DispatchFrame(json);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                QueueMain(() => ErrorReceived?.Invoke("ipc_receive_failed", exception.Message));
            }
            finally
            {
                QueueMain(() => ConnectionChanged?.Invoke(false, "Disconnected"));
            }
        }

        private static async Task<string> ReadFrameAsync(
            Stream stream,
            CancellationToken cancellationToken)
        {
            var header = new byte[sizeof(int)];
            if (!await ReadExactlyOrEndAsync(stream, header, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(header);
            }

            var length = BitConverter.ToInt32(header, 0);
            if (length <= 0 || length > IpcProtocol.MaximumPayloadBytes)
            {
                throw new InvalidDataException("Core sent an invalid IPC frame length.");
            }

            var payload = new byte[length];
            if (!await ReadExactlyOrEndAsync(stream, payload, cancellationToken).ConfigureAwait(false))
            {
                throw new EndOfStreamException("Core disconnected during an IPC frame.");
            }

            return Encoding.UTF8.GetString(payload);
        }

        private static async Task<bool> ReadExactlyOrEndAsync(
            Stream stream,
            byte[] buffer,
            CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = await stream.ReadAsync(
                    buffer,
                    offset,
                    buffer.Length - offset,
                    cancellationToken).ConfigureAwait(false);
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

        private void DispatchFrame(string json)
        {
            var header = JsonUtility.FromJson<EnvelopeHeader>(json);
            if (header == null || string.IsNullOrWhiteSpace(header.messageType))
            {
                QueueMain(() => ErrorReceived?.Invoke("invalid_envelope", "Core sent an invalid envelope."));
                return;
            }

            if (!string.Equals(header.contractVersion, IpcProtocol.Version, StringComparison.Ordinal))
            {
                QueueMain(() => ErrorReceived?.Invoke(
                    "contract_version_mismatch",
                    "Unity client and core use different IPC contract versions."));
                return;
            }

            switch (header.messageType)
            {
                case IpcProtocol.HealthResponse:
                {
                    var envelope = JsonUtility.FromJson<HealthResponseEnvelope>(json);
                    QueueMain(() => HealthChanged?.Invoke(envelope.payload.status));
                    break;
                }
                case IpcProtocol.ChatStarted:
                {
                    var envelope = JsonUtility.FromJson<ChatStartedEnvelope>(json);
                    QueueMain(() => ChatStarted?.Invoke(envelope.payload.requestId));
                    break;
                }
                case IpcProtocol.ChatChunk:
                {
                    var envelope = JsonUtility.FromJson<ChatChunkEnvelope>(json);
                    QueueMain(() => ChatChunkReceived?.Invoke(
                        envelope.payload.requestId,
                        envelope.payload.text));
                    break;
                }
                case IpcProtocol.ChatCompleted:
                {
                    var envelope = JsonUtility.FromJson<ChatCompletedEnvelope>(json);
                    QueueMain(() =>
                    {
                        _currentRequestId = string.Empty;
                        ChatCompleted?.Invoke(
                            envelope.payload.requestId,
                            envelope.payload.text,
                            envelope.payload.durationMilliseconds);
                    });
                    break;
                }
                case IpcProtocol.AvatarState:
                {
                    var envelope = JsonUtility.FromJson<AvatarStateEnvelope>(json);
                    QueueMain(() => AvatarStateReceived?.Invoke(
                        envelope.payload.state,
                        envelope.payload.detail));
                    break;
                }
                case IpcProtocol.TaskCancelled:
                {
                    var envelope = JsonUtility.FromJson<TaskCancelledEnvelope>(json);
                    QueueMain(() =>
                    {
                        _currentRequestId = string.Empty;
                        TaskCancelled?.Invoke(envelope.payload.requestId);
                    });
                    break;
                }
                case IpcProtocol.Error:
                {
                    var envelope = JsonUtility.FromJson<ErrorEnvelope>(json);
                    QueueMain(() => ErrorReceived?.Invoke(
                        envelope.payload.code,
                        envelope.payload.message));
                    break;
                }
                default:
                    QueueMain(() => ErrorReceived?.Invoke(
                        "unknown_message",
                        "Unknown IPC message: " + header.messageType));
                    break;
            }
        }

        private void QueueMain(Action action)
        {
            if (!_destroyed)
            {
                _mainThreadActions.Enqueue(action);
            }
        }

        private void OnDestroy()
        {
            _destroyed = true;
            lock (_connectionSync)
            {
                _connectionCancellation?.Cancel();
                _pipe?.Dispose();
                _pipe = null;
            }

            _sendLock.Dispose();
        }
    }
}
