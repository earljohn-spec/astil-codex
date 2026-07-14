using System.Collections.Generic;
using UnityEngine;

namespace AstilCodex.UnityClient.Avatar
{
    [DisallowMultipleComponent]
    public sealed class PlaceholderAvatarController : MonoBehaviour
    {
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private Transform _visualRoot;
        private Transform _leftShard;
        private Transform _rightShard;
        private Material _material;
        private Color _currentColor = new Color(0.55f, 0.39f, 1f);
        private Color _targetColor = new Color(0.55f, 0.39f, 1f);
        private float _motionSpeed = 1f;
        private string _state = "ready";

        public string CurrentState
        {
            get { return _state; }
        }

        private void Awake()
        {
            BuildPlaceholder();
        }

        private void Update()
        {
            if (_visualRoot == null)
            {
                return;
            }

            var time = Time.unscaledTime;
            _visualRoot.localPosition = new Vector3(
                0f,
                Mathf.Sin(time * 1.4f * _motionSpeed) * 0.035f,
                0f);
            _visualRoot.localRotation = Quaternion.Euler(
                0f,
                Mathf.Sin(time * 0.45f) * 3f,
                0f);

            AnimateShard(_leftShard, time, -1f);
            AnimateShard(_rightShard, time, 1f);

            _currentColor = Color.Lerp(_currentColor, _targetColor, Time.unscaledDeltaTime * 5f);
            ApplyMaterialColor(_currentColor);
        }

        public void SetState(string state)
        {
            _state = string.IsNullOrWhiteSpace(state) ? "ready" : state.ToLowerInvariant();
            switch (_state)
            {
                case "listening":
                    _targetColor = new Color(0.25f, 0.94f, 0.86f);
                    _motionSpeed = 1.4f;
                    break;
                case "thinking":
                    _targetColor = new Color(1f, 0.68f, 0.28f);
                    _motionSpeed = 1.8f;
                    break;
                case "speaking":
                    _targetColor = new Color(0.65f, 0.43f, 1f);
                    _motionSpeed = 2.1f;
                    break;
                case "acting":
                    _targetColor = new Color(0.35f, 0.65f, 1f);
                    _motionSpeed = 2.5f;
                    break;
                case "success":
                    _targetColor = new Color(0.32f, 0.92f, 0.58f);
                    _motionSpeed = 1.2f;
                    break;
                case "error":
                case "cancelled":
                    _targetColor = new Color(1f, 0.28f, 0.4f);
                    _motionSpeed = 0.55f;
                    break;
                default:
                    _targetColor = new Color(0.55f, 0.39f, 1f);
                    _motionSpeed = 1f;
                    break;
            }
        }

        public void SetVisible(bool visible)
        {
            if (_visualRoot != null)
            {
                _visualRoot.gameObject.SetActive(visible);
            }
        }

        private void BuildPlaceholder()
        {
            _visualRoot = new GameObject("Synthetic Avatar Placeholder").transform;
            _visualRoot.SetParent(transform, false);

            var shader = Resources.Load<Shader>("AstilPlaceholder");
            if (shader == null)
            {
                Debug.LogError(
                    "AstilPlaceholder shader was not included in the player. " +
                    "Verify Assets/AstilCodex/Resources/AstilPlaceholder.shader.");
                enabled = false;
                return;
            }

            _material = new Material(shader) { name = "Astil Placeholder Material" };

            CreatePart(PrimitiveType.Capsule, "Body", new Vector3(0f, 0.85f, 0f), new Vector3(0.72f, 1.05f, 0.48f));
            CreatePart(PrimitiveType.Sphere, "Head", new Vector3(0f, 2.05f, 0f), new Vector3(0.73f, 0.83f, 0.68f));
            CreatePart(PrimitiveType.Cube, "Collar", new Vector3(0f, 1.42f, 0f), new Vector3(0.92f, 0.18f, 0.55f));
            CreatePart(PrimitiveType.Cube, "Coat Left", new Vector3(-0.42f, 0.68f, 0f), new Vector3(0.2f, 1.25f, 0.5f), new Vector3(0f, 0f, -7f));
            CreatePart(PrimitiveType.Cube, "Coat Right", new Vector3(0.42f, 0.68f, 0f), new Vector3(0.2f, 1.25f, 0.5f), new Vector3(0f, 0f, 7f));

            _leftShard = CreatePart(
                PrimitiveType.Cube,
                "Codex Shard Left",
                new Vector3(-1.15f, 1.45f, 0f),
                new Vector3(0.18f, 0.62f, 0.12f),
                new Vector3(15f, 0f, -18f)).transform;
            _rightShard = CreatePart(
                PrimitiveType.Cube,
                "Codex Shard Right",
                new Vector3(1.15f, 1.45f, 0f),
                new Vector3(0.18f, 0.62f, 0.12f),
                new Vector3(-15f, 0f, 18f)).transform;

            ApplyMaterialColor(_currentColor);
        }

        private GameObject CreatePart(
            PrimitiveType primitive,
            string partName,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3? localEuler = null)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = partName;
            part.transform.SetParent(_visualRoot, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localEulerAngles = localEuler ?? Vector3.zero;
            var collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = _material;
            _renderers.Add(renderer);
            return part;
        }

        private void AnimateShard(Transform shard, float time, float direction)
        {
            if (shard == null)
            {
                return;
            }

            var angle = time * 42f * _motionSpeed * direction;
            var radians = angle * Mathf.Deg2Rad;
            shard.localPosition = new Vector3(
                direction * (1.1f + Mathf.Sin(time * 1.7f) * 0.08f),
                1.42f + Mathf.Cos(time * 1.5f) * 0.12f,
                Mathf.Sin(radians) * 0.18f);
            shard.localRotation = Quaternion.Euler(angle * 0.25f, angle, direction * 18f);
        }

        private void ApplyMaterialColor(Color color)
        {
            if (_material == null)
            {
                return;
            }

            if (_material.HasProperty("_BaseColor"))
            {
                _material.SetColor("_BaseColor", color);
            }
            else if (_material.HasProperty("_Color"))
            {
                _material.SetColor("_Color", color);
            }

            if (_material.HasProperty("_EmissionColor"))
            {
                _material.EnableKeyword("_EMISSION");
                _material.SetColor("_EmissionColor", color * 0.35f);
            }
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }
        }
    }
}
