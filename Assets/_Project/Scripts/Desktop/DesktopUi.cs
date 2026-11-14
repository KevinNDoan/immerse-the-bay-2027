using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zap.Core;

namespace Zap.Desktop
{
    public static class DesktopUi
    {
        public static Image CreatePanel(string name, Transform parent, ZapTheme theme)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            var color = theme.voidColor;
            color.a = .85f;
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI CreateText(string name, Transform parent, string value, float size, TextAlignmentOptions alignment, ZapTheme theme)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.richText = false;
            if (theme.font != null) text.font = theme.font;
            return text;
        }

        public static Button CreateButton(string name, Transform parent, string label, ZapTheme theme)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, .1f);
            var button = obj.AddComponent<Button>();
            button.targetGraphic = image;
            var text = CreateText("Label", obj.transform, label, 18f, TextAlignmentOptions.Center, theme);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return button;
        }

        public static TMP_InputField CreateInputField(string name, Transform parent, string placeholderValue, ZapTheme theme)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, .1f);
            var outline = obj.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, .3f);
            outline.effectDistance = new Vector2(1f, -1f);
            var input = obj.AddComponent<TMP_InputField>();
            input.targetGraphic = image;

            var textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(obj.transform, false);
            var viewport = (RectTransform)textArea.transform;
            SetRect(viewport, Vector2.zero, Vector2.one, new Vector2(18f, 8f), new Vector2(-36f, -16f));

            var placeholder = CreateText("Placeholder", textArea.transform, placeholderValue, 20f, TextAlignmentOptions.MidlineLeft, theme);
            placeholder.color = new Color(1f, 1f, 1f, .5f);
            SetRect(placeholder.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var text = CreateText("Text", textArea.transform, string.Empty, 20f, TextAlignmentOptions.MidlineLeft, theme);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.caretColor = Color.white;
            input.selectionColor = new Color(1f, 1f, 1f, .25f);
            return input;
        }

        public static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        public static void SetText(TMP_Text target, string value)
        {
            value ??= string.Empty;
            if (target.text != value) target.text = value;
        }

        public static void SetActive(GameObject target, bool value)
        {
            if (target.activeSelf != value) target.SetActive(value);
        }
    }
}
