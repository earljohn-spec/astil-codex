using AstilCodex.UnityClient.Avatar;
using AstilCodex.UnityClient.Ipc;
using UnityEngine;

namespace AstilCodex.UnityClient
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class AstilRuntimeBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 1;
            BuildEnvironment();
            EnsureComponent<AstilIpcClient>(gameObject);
            EnsureComponent<CoreHostLauncher>(gameObject);
            EnsureComponent<AstilAppController>(gameObject);
        }

        private static void BuildEnvironment()
        {
            var camera = FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                var cameraObject = new GameObject("Astil Camera", typeof(Camera), typeof(AudioListener));
                camera = cameraObject.GetComponent<Camera>();
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.024f, 0.055f, 1f);
            camera.transform.position = new Vector3(-0.45f, 1.35f, 6.4f);
            camera.transform.LookAt(new Vector3(-0.45f, 1.25f, 0f));
            camera.fieldOfView = 38f;

            if (FindFirstObjectByType<Light>() == null)
            {
                var lightObject = new GameObject("Key Light", typeof(Light));
                var light = lightObject.GetComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(0.74f, 0.82f, 1f);
                light.intensity = 1.25f;
                lightObject.transform.rotation = Quaternion.Euler(38f, -28f, 0f);

                var fillObject = new GameObject("Fill Light", typeof(Light));
                var fill = fillObject.GetComponent<Light>();
                fill.type = LightType.Point;
                fill.color = new Color(0.42f, 0.24f, 0.95f);
                fill.intensity = 3.2f;
                fill.range = 9f;
                fillObject.transform.position = new Vector3(-2.2f, 2.6f, 2.4f);
            }

            if (FindFirstObjectByType<PlaceholderAvatarController>() == null)
            {
                var avatarRoot = new GameObject("Avatar Root");
                avatarRoot.transform.position = new Vector3(-0.45f, -0.55f, 0f);
                avatarRoot.AddComponent<PlaceholderAvatarController>();
                avatarRoot.AddComponent<VrmAvatarLoader>();
            }

            if (GameObject.Find("Avatar Platform") == null)
            {
                var platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                platform.name = "Avatar Platform";
                platform.transform.position = new Vector3(-0.45f, -0.62f, 0f);
                platform.transform.localScale = new Vector3(1.45f, 0.05f, 1.45f);
                var collider = platform.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                var renderer = platform.GetComponent<Renderer>();
                var shader = Resources.Load<Shader>("AstilPlaceholder");
                if (shader != null)
                {
                    var material = new Material(shader);
                    if (material.HasProperty("_BaseColor"))
                    {
                        material.SetColor("_BaseColor", new Color(0.08f, 0.12f, 0.2f));
                    }

                    renderer.material = material;
                }
                else
                {
                    Debug.LogError("AstilPlaceholder shader resource is missing.");
                }
            }
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }
    }
}
