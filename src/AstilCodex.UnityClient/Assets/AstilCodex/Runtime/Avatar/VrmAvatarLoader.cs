using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UniVRM10;
using UnityEngine;

namespace AstilCodex.UnityClient.Avatar
{
    [DisallowMultipleComponent]
    public sealed class VrmAvatarLoader : MonoBehaviour
    {
        private CancellationTokenSource _loadCancellation;
        private Vrm10Instance _loadedInstance;

        public event Action<GameObject, string> AvatarLoaded;
        public event Action<string> AvatarLoadFailed;

        public bool HasLoadedAvatar
        {
            get { return _loadedInstance != null; }
        }

        public string DefaultAvatarPath
        {
            get
            {
                var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(localData, "AstilCodex", "avatars", "astil.vrm");
            }
        }

        public Task<bool> LoadDefaultAvatarAsync()
        {
            return LoadAvatarAsync(DefaultAvatarPath);
        }

        public async Task<bool> LoadAvatarAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                AvatarLoadFailed?.Invoke("VRM file not found: " + path);
                return false;
            }

            if (!string.Equals(Path.GetExtension(path), ".vrm", StringComparison.OrdinalIgnoreCase))
            {
                AvatarLoadFailed?.Invoke("Only .vrm avatar files are accepted.");
                return false;
            }

            const long maximumAvatarBytes = 256L * 1024L * 1024L;
            if (new FileInfo(path).Length > maximumAvatarBytes)
            {
                AvatarLoadFailed?.Invoke("VRM file exceeds the 256 MiB safety limit.");
                return false;
            }

            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = new CancellationTokenSource();

            try
            {
                var instance = await Vrm10.LoadPathAsync(
                    path,
                    canLoadVrm0X: true,
                    showMeshes: true,
                    ct: _loadCancellation.Token);
                if (instance == null)
                {
                    AvatarLoadFailed?.Invoke("UniVRM returned no avatar instance.");
                    return false;
                }

                DestroyLoadedAvatar();
                _loadedInstance = instance;
                instance.transform.SetParent(transform, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                AvatarLoaded?.Invoke(instance.gameObject, path);
                return true;
            }
            catch (OperationCanceledException)
            {
                AvatarLoadFailed?.Invoke("VRM loading was cancelled.");
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                AvatarLoadFailed?.Invoke(exception.Message);
                return false;
            }
        }

        private void DestroyLoadedAvatar()
        {
            if (_loadedInstance == null)
            {
                return;
            }

            Destroy(_loadedInstance.gameObject);
            _loadedInstance = null;
        }

        private void OnDestroy()
        {
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            DestroyLoadedAvatar();
        }
    }
}
