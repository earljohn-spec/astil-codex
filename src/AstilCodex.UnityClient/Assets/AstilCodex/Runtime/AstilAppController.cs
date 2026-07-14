using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using AstilCodex.UnityClient.Avatar;
using AstilCodex.UnityClient.Ipc;
using AstilCodex.UnityClient.UI;
using UnityEngine;
using UnityEngine.UI;

namespace AstilCodex.UnityClient
{
    [DisallowMultipleComponent]
    public sealed class AstilAppController : MonoBehaviour
    {
        private const string SessionId = "unity-development";
        private readonly StringBuilder _chat = new StringBuilder();
        private readonly string[] _policies =
        {
            "autoPrivacyFirst", "localOnly", "cloudPreferred", "askEveryTime"
        };
        private readonly string[] _policyLabels =
        {
            "Privacy: Auto", "Privacy: Local", "Privacy: Cloud", "Privacy: Ask"
        };
        private readonly Dictionary<string, string> _modeDescriptions =
            new Dictionary<string, string>
            {
                { "companion", "Conversation and everyday check-ins." },
                { "assistant", "Planning, reminders, and document assistance." },
                { "focus", "Concise answers with reduced distractions." },
                { "developer", "Code analysis inside approved workspaces." },
                { "creator", "Blender and 3D creation planning." }
            };

        private AstilIpcClient _ipc;
        private CoreHostLauncher _hostLauncher;
        private PlaceholderAvatarController _placeholder;
        private VrmAvatarLoader _vrmLoader;
        private AstilUiReferences _ui;
        private string _mode = "companion";
        private int _policyIndex;
        private bool _receivedChunk;
        private bool _sending;

        private void Start()
        {
            _ipc = GetComponent<AstilIpcClient>();
            _hostLauncher = GetComponent<CoreHostLauncher>();
            _placeholder = FindFirstObjectByType<PlaceholderAvatarController>();
            _vrmLoader = FindFirstObjectByType<VrmAvatarLoader>();
            _ui = AstilUiFactory.Create();
            _chat.AppendLine("Astil: Unity client ready. Connect to the local core to begin.");
            RefreshChat();
            BindUi();
            SubscribeToCore();
            SubscribeToAvatar();
            SelectMode(_mode);
        }

        private void BindUi()
        {
            _ui.ConnectButton.onClick.AddListener(ConnectClicked);
            _ui.SendButton.onClick.AddListener(SendClicked);
            _ui.StopButton.onClick.AddListener(StopClicked);
            _ui.LoadAvatarButton.onClick.AddListener(LoadAvatarClicked);
            _ui.PolicyButton.onClick.AddListener(CyclePolicy);
            _ui.MessageInput.onEndEdit.AddListener(InputSubmitted);
            foreach (var pair in _ui.ModeButtons)
            {
                var captured = pair.Key;
                pair.Value.onClick.AddListener(() => SelectMode(captured));
            }
        }

        private void SubscribeToCore()
        {
            _ipc.ConnectionChanged += OnConnectionChanged;
            _ipc.HealthChanged += OnHealthChanged;
            _ipc.ChatStarted += OnChatStarted;
            _ipc.ChatChunkReceived += OnChatChunk;
            _ipc.ChatCompleted += OnChatCompleted;
            _ipc.AvatarStateReceived += OnAvatarState;
            _ipc.TaskCancelled += OnTaskCancelled;
            _ipc.ErrorReceived += OnCoreError;
        }

        private void SubscribeToAvatar()
        {
            if (_vrmLoader == null)
            {
                return;
            }

            _vrmLoader.AvatarLoaded += OnAvatarLoaded;
            _vrmLoader.AvatarLoadFailed += OnAvatarLoadFailed;
        }

        private async void ConnectClicked()
        {
            if (_ipc.IsConnected)
            {
                await _ipc.DisconnectAsync();
                return;
            }

            SetCoreStatus("Starting local core…");
            _hostLauncher.TryStartCoreHost();
            await Task.Delay(500);
            var connected = await _ipc.ConnectToCoreAsync(TimeSpan.FromSeconds(5));
            if (!connected)
            {
                SetCoreStatus(
                    "Core unavailable. Build AstilCodex.Core.Host in Release mode, then retry.");
            }
        }

        private async void SendClicked()
        {
            await SendCurrentInputAsync();
        }

        private async void InputSubmitted(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                await SendCurrentInputAsync();
            }
        }

        private async Task SendCurrentInputAsync()
        {
            if (_sending)
            {
                return;
            }

            var message = _ui.MessageInput.text.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (!_ipc.IsConnected)
            {
                AppendLine("System: Connect to the local core before sending a message.");
                return;
            }

            _sending = true;
            _receivedChunk = false;
            _ui.MessageInput.text = string.Empty;
            AppendLine("You: " + message);
            try
            {
                await _ipc.SendChatAsync(
                    SessionId,
                    _mode,
                    message,
                    _policies[_policyIndex]);
            }
            catch (Exception exception)
            {
                AppendLine("System: " + exception.Message);
                _sending = false;
            }
        }

        private async void StopClicked()
        {
            try
            {
                await _ipc.CancelCurrentTaskAsync();
            }
            catch (Exception exception)
            {
                AppendLine("System: Unable to stop task: " + exception.Message);
            }
        }

        private async void LoadAvatarClicked()
        {
            if (_vrmLoader == null)
            {
                AppendLine("System: VRM loader is unavailable.");
                return;
            }

            SetCoreStatus("Loading VRM from " + _vrmLoader.DefaultAvatarPath);
            await _vrmLoader.LoadDefaultAvatarAsync();
        }

        private void CyclePolicy()
        {
            _policyIndex = (_policyIndex + 1) % _policies.Length;
            _ui.PolicyText.text = _policyLabels[_policyIndex];
            AppendLine("System: Processing policy changed to " + _policyLabels[_policyIndex] + ".");
        }

        private void SelectMode(string mode)
        {
            _mode = mode;
            AstilUiFactory.SetModeVisuals(_ui, mode);
            _ui.ModeDescriptionText.text = _modeDescriptions[mode];
            SetCoreStatus("Mode: " + char.ToUpperInvariant(mode[0]) + mode.Substring(1));
        }

        private void OnConnectionChanged(bool connected, string detail)
        {
            _ui.ConnectionText.text = connected ? "● Connected" : "● " + detail;
            _ui.ConnectionText.color = connected
                ? new Color(0.32f, 0.92f, 0.58f)
                : new Color(0.7f, 0.72f, 0.8f);
            _ui.ConnectButton.GetComponentInChildren<Text>().text = connected
                ? "Disconnect Core"
                : "Connect Core";
            SetCoreStatus(detail);
        }

        private void OnHealthChanged(string status)
        {
            SetCoreStatus("Core health: " + status);
        }

        private void OnChatStarted(string requestId)
        {
            _receivedChunk = false;
            AppendRaw("Astil: ");
            SetCoreStatus("Receiving " + requestId.Substring(0, Mathf.Min(8, requestId.Length)) + "…");
        }

        private void OnChatChunk(string requestId, string chunk)
        {
            _receivedChunk = true;
            AppendRaw(chunk);
        }

        private void OnChatCompleted(
            string requestId,
            string finalText,
            float durationMilliseconds,
            string providerId)
        {
            if (!_receivedChunk && !string.IsNullOrWhiteSpace(finalText))
            {
                AppendRaw(finalText);
            }

            AppendRaw("\n");
            _sending = false;
            var providerLabel = string.IsNullOrWhiteSpace(providerId) ? "unknown" : providerId;
            SetCoreStatus(
                "Provider: " + providerLabel +
                " · completed in " + Mathf.RoundToInt(durationMilliseconds) + " ms");
        }

        private void OnAvatarState(string state, string detail)
        {
            _placeholder?.SetState(state);
            _ui.AvatarStateText.text = "AVATAR · " + state.ToUpperInvariant();
            SetCoreStatus(detail);
        }

        private void OnTaskCancelled(string requestId)
        {
            AppendLine("System: Task cancelled.");
            _sending = false;
            _placeholder?.SetState("cancelled");
        }

        private void OnCoreError(string code, string message)
        {
            AppendLine("System error [" + code + "]: " + message);
            _sending = false;
            _placeholder?.SetState("error");
        }

        private void OnAvatarLoaded(GameObject avatar, string path)
        {
            _placeholder?.SetVisible(false);
            AppendLine("System: Loaded VRM avatar from " + path);
            SetCoreStatus("VRM avatar active");
        }

        private void OnAvatarLoadFailed(string message)
        {
            _placeholder?.SetVisible(true);
            AppendLine("System: " + message);
            SetCoreStatus("Using synthetic placeholder avatar");
        }

        private void SetCoreStatus(string value)
        {
            if (_ui != null && _ui.CoreText != null)
            {
                _ui.CoreText.text = value;
            }
        }

        private void AppendLine(string value)
        {
            _chat.AppendLine(value);
            TrimChat();
            RefreshChat();
        }

        private void AppendRaw(string value)
        {
            _chat.Append(value);
            TrimChat();
            RefreshChat();
        }

        private void TrimChat()
        {
            const int maximumCharacters = 24000;
            if (_chat.Length > maximumCharacters)
            {
                _chat.Remove(0, _chat.Length - maximumCharacters);
            }
        }

        private void RefreshChat()
        {
            if (_ui == null || _ui.ChatText == null)
            {
                return;
            }

            _ui.ChatText.text = _chat.ToString();
            Canvas.ForceUpdateCanvases();
            _ui.ChatScroll.verticalNormalizedPosition = 0f;
        }

        private void OnDestroy()
        {
            if (_ipc != null)
            {
                _ipc.ConnectionChanged -= OnConnectionChanged;
                _ipc.HealthChanged -= OnHealthChanged;
                _ipc.ChatStarted -= OnChatStarted;
                _ipc.ChatChunkReceived -= OnChatChunk;
                _ipc.ChatCompleted -= OnChatCompleted;
                _ipc.AvatarStateReceived -= OnAvatarState;
                _ipc.TaskCancelled -= OnTaskCancelled;
                _ipc.ErrorReceived -= OnCoreError;
            }

            if (_vrmLoader != null)
            {
                _vrmLoader.AvatarLoaded -= OnAvatarLoaded;
                _vrmLoader.AvatarLoadFailed -= OnAvatarLoadFailed;
            }
        }
    }
}
