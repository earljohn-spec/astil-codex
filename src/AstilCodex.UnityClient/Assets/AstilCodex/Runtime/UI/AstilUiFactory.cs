using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AstilCodex.UnityClient.UI
{
    public sealed class AstilUiReferences
    {
        public Text ConnectionText;
        public Text CoreText;
        public Text AvatarStateText;
        public Text ChatText;
        public Text ModeDescriptionText;
        public Text PolicyText;
        public InputField MessageInput;
        public Button ConnectButton;
        public Button SendButton;
        public Button StopButton;
        public Button LoadAvatarButton;
        public Button PolicyButton;
        public ScrollRect ChatScroll;
        public readonly Dictionary<string, Button> ModeButtons = new Dictionary<string, Button>();
    }

    public static class AstilUiFactory
    {
        private static readonly Color Panel = new Color(0.055f, 0.067f, 0.12f, 0.94f);
        private static readonly Color PanelSoft = new Color(0.075f, 0.09f, 0.16f, 0.88f);
        private static readonly Color Line = new Color(0.34f, 0.39f, 0.58f, 0.22f);
        private static readonly Color Ink = new Color(0.92f, 0.94f, 1f, 1f);
        private static readonly Color Muted = new Color(0.56f, 0.61f, 0.74f, 1f);
        private static readonly Color Violet = new Color(0.53f, 0.39f, 0.95f, 1f);
        private static readonly Color Cyan = new Color(0.33f, 0.88f, 0.82f, 1f);
        private static Font _font;

        public static AstilUiReferences Create()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();

            var canvasObject = new GameObject("Astil UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var references = new AstilUiReferences();
            CreateTopBar(canvasObject.transform, references);
            CreateModeRail(canvasObject.transform, references);
            CreateChatPanel(canvasObject.transform, references);
            CreateComposer(canvasObject.transform, references);
            CreateSceneStatus(canvasObject.transform, references);
            return references;
        }

        public static void SetModeVisuals(AstilUiReferences ui, string activeMode)
        {
            foreach (var pair in ui.ModeButtons)
            {
                var image = pair.Value.GetComponent<Image>();
                image.color = pair.Key == activeMode
                    ? new Color(Violet.r, Violet.g, Violet.b, 0.42f)
                    : new Color(1f, 1f, 1f, 0.035f);
            }
        }

        private static void CreateTopBar(Transform parent, AstilUiReferences ui)
        {
            var bar = CreatePanel(parent, "Top Bar", Panel);
            SetRect(bar.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -54f), Vector2.zero);

            var brand = CreateText(bar.transform, "ASTIL CODEX", 18, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
            SetRect(brand.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(24f, 0f), new Vector2(260f, 0f));

            var build = CreateText(bar.transform, "UNITY CLIENT · PRE-ALPHA", 11, FontStyle.Normal, TextAnchor.MiddleLeft, Muted);
            SetRect(build.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(265f, 0f), new Vector2(480f, 0f));

            ui.ConnectionText = CreateText(bar.transform, "● Disconnected", 12, FontStyle.Normal, TextAnchor.MiddleRight, Muted);
            SetRect(ui.ConnectionText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-520f, 0f), new Vector2(-230f, 0f));

            ui.ConnectButton = CreateButton(bar.transform, "Connect Core", new Color(Cyan.r, Cyan.g, Cyan.b, 0.16f));
            SetRect(ui.ConnectButton.GetComponent<RectTransform>(), new Vector2(1f, 0.15f), new Vector2(1f, 0.85f), new Vector2(-210f, 0f), new Vector2(-20f, 0f));
        }

        private static void CreateModeRail(Transform parent, AstilUiReferences ui)
        {
            var rail = CreatePanel(parent, "Mode Rail", PanelSoft);
            SetRect(rail.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(190f, -54f));

            var heading = CreateText(rail.transform, "MODES", 10, FontStyle.Bold, TextAnchor.MiddleLeft, Muted);
            SetRect(heading.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -49f), new Vector2(-12f, -18f));

            var modes = new[] { "companion", "assistant", "focus", "developer", "creator" };
            var labels = new[] { "Companion", "Assistant", "Focus", "Developer", "Creator" };
            for (var index = 0; index < modes.Length; index++)
            {
                var button = CreateButton(rail.transform, labels[index], new Color(1f, 1f, 1f, 0.035f));
                var top = -72f - index * 58f;
                SetRect(button.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, top - 44f), new Vector2(-14f, top));
                ui.ModeButtons[modes[index]] = button;
            }

            ui.ModeDescriptionText = CreateText(
                rail.transform,
                "Conversation and everyday check-ins.",
                11,
                FontStyle.Normal,
                TextAnchor.UpperLeft,
                Muted);
            SetRect(ui.ModeDescriptionText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 145f), new Vector2(-16f, 230f));

            ui.PolicyButton = CreateButton(rail.transform, "Privacy: Auto", new Color(Cyan.r, Cyan.g, Cyan.b, 0.1f));
            SetRect(ui.PolicyButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14f, 86f), new Vector2(-14f, 132f));
            ui.PolicyText = ui.PolicyButton.GetComponentInChildren<Text>();

            ui.LoadAvatarButton = CreateButton(rail.transform, "Load default VRM", new Color(Violet.r, Violet.g, Violet.b, 0.13f));
            SetRect(ui.LoadAvatarButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14f, 30f), new Vector2(-14f, 76f));
        }

        private static void CreateChatPanel(Transform parent, AstilUiReferences ui)
        {
            var panel = CreatePanel(parent, "Conversation Panel", PanelSoft);
            SetRect(panel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-540f, 92f), new Vector2(-20f, -72f));

            var title = CreateText(panel.transform, "CONVERSATION", 11, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -48f), new Vector2(-18f, -12f));

            var viewport = CreatePanel(panel.transform, "Viewport", new Color(0f, 0f, 0f, 0.08f));
            SetRect(viewport.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(14f, 14f), new Vector2(-14f, -54f));
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = new GameObject(
                "Chat Content",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            SetRect(contentRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, 0f), new Vector2(-10f, 0f));
            contentRect.pivot = new Vector2(0.5f, 1f);
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ui.ChatText = CreateText(content.transform, "Astil: Unity client ready. Connect to the local core to begin.\n", 14, FontStyle.Normal, TextAnchor.UpperLeft, Ink);
            SetRect(ui.ChatText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            ui.ChatText.horizontalOverflow = HorizontalWrapMode.Wrap;
            ui.ChatText.verticalOverflow = VerticalWrapMode.Overflow;
            ui.ChatText.supportRichText = false;

            ui.ChatScroll = panel.gameObject.AddComponent<ScrollRect>();
            ui.ChatScroll.viewport = viewport.rectTransform;
            ui.ChatScroll.content = contentRect;
            ui.ChatScroll.horizontal = false;
            ui.ChatScroll.vertical = true;
            ui.ChatScroll.movementType = ScrollRect.MovementType.Clamped;
            ui.ChatScroll.scrollSensitivity = 28f;
        }

        private static void CreateComposer(Transform parent, AstilUiReferences ui)
        {
            var composer = CreatePanel(parent, "Composer", Panel);
            SetRect(composer.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(210f, 18f), new Vector2(-20f, 78f));

            ui.MessageInput = CreateInputField(composer.transform);
            SetRect(ui.MessageInput.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 10f), new Vector2(-228f, -10f));

            ui.StopButton = CreateButton(composer.transform, "Stop", new Color(0.85f, 0.2f, 0.3f, 0.2f));
            SetRect(ui.StopButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-210f, 10f), new Vector2(-118f, -10f));

            ui.SendButton = CreateButton(composer.transform, "Send", Violet);
            SetRect(ui.SendButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-108f, 10f), new Vector2(-10f, -10f));
        }

        private static void CreateSceneStatus(Transform parent, AstilUiReferences ui)
        {
            var card = CreatePanel(parent, "Scene Status", new Color(0.035f, 0.045f, 0.08f, 0.72f));
            SetRect(card.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-225f, -116f), new Vector2(225f, -70f));

            ui.AvatarStateText = CreateText(card.transform, "AVATAR · READY", 12, FontStyle.Bold, TextAnchor.MiddleCenter, Cyan);
            SetRect(ui.AvatarStateText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            ui.CoreText = CreateText(parent, "Core host is not connected", 11, FontStyle.Normal, TextAnchor.MiddleCenter, Muted);
            SetRect(ui.CoreText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-280f, 90f), new Vector2(280f, 116f));
        }

        private static Image CreatePanel(Transform parent, string name, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(
            Transform parent,
            string value,
            int size,
            FontStyle style,
            TextAnchor alignment,
            Color color)
        {
            var gameObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            var text = gameObject.GetComponent<Text>();
            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(Transform parent, string label, Color color)
        {
            var gameObject = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            var button = gameObject.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.78f);
            colors.pressedColor = new Color(0.76f, 0.72f, 1f, 0.72f);
            button.colors = colors;

            var text = CreateText(gameObject.transform, label, 12, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
            return button;
        }

        private static InputField CreateInputField(Transform parent)
        {
            var gameObject = new GameObject("Message Input", typeof(RectTransform), typeof(Image), typeof(InputField));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.055f);

            var inputText = CreateText(gameObject.transform, string.Empty, 14, FontStyle.Normal, TextAnchor.MiddleLeft, Ink);
            SetRect(inputText.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 4f), new Vector2(-14f, -4f));
            inputText.supportRichText = false;

            var placeholder = CreateText(gameObject.transform, "Message Astil Codex…", 14, FontStyle.Italic, TextAnchor.MiddleLeft, Muted);
            SetRect(placeholder.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 4f), new Vector2(-14f, -4f));

            var field = gameObject.GetComponent<InputField>();
            field.textComponent = inputText;
            field.placeholder = placeholder;
            field.lineType = InputField.LineType.SingleLine;
            field.targetGraphic = image;
            field.caretColor = Cyan;
            field.selectionColor = new Color(Violet.r, Violet.g, Violet.b, 0.4f);
            return field;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }
}
