using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AstilCodex.UnityClient
{
    [DisallowMultipleComponent]
    public sealed class CoreHostLauncher : MonoBehaviour
    {
        private Process _ownedProcess;

        public bool IsOwnedProcessRunning
        {
            get { return _ownedProcess != null && !_ownedProcess.HasExited; }
        }

        public string LastResolvedPath { get; private set; } = string.Empty;

        public bool TryStartCoreHost()
        {
            if (IsOwnedProcessRunning)
            {
                return true;
            }

            var path = ResolveCoreHostPath();
            LastResolvedPath = path;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Debug.LogWarning(
                    "Astil Codex Core Host was not found. Build it in Release mode or set " +
                    "ASTIL_CODEX_CORE_HOST to the full executable path.");
                return false;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = path,
                    WorkingDirectory = Path.GetDirectoryName(path) ?? string.Empty,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                _ownedProcess = Process.Start(startInfo);
                return _ownedProcess != null;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        public string ResolveCoreHostPath()
        {
            var configured = Environment.GetEnvironmentVariable("ASTIL_CODEX_CORE_HOST");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return Path.GetFullPath(configured);
            }

#if UNITY_EDITOR_WIN
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "..",
                "AstilCodex.Core.Host",
                "bin",
                "Release",
                "net8.0",
                "astil-core-host.exe"));
#elif UNITY_STANDALONE_WIN
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Core",
                "astil-core-host.exe"));
#else
            return string.Empty;
#endif
        }

        private void OnDestroy()
        {
            if (_ownedProcess == null)
            {
                return;
            }

            try
            {
                if (!_ownedProcess.HasExited)
                {
                    _ownedProcess.Kill();
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Unable to stop owned core process: " + exception.Message);
            }
            finally
            {
                _ownedProcess.Dispose();
                _ownedProcess = null;
            }
        }
    }
}
