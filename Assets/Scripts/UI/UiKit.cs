using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// 코드로 uGUI를 만드는 도우미. 기준 해상도 750×1334(세로), 좌표는 왼쪽 위 기준 픽셀.
// TMP 기본 리소스가 프로젝트에 없어서 기본 uGUI(Text·InputField)와 내장 폰트를 쓴다.
public static class UiKit
{
    public const float RefWidth = 750f;
    public const float RefHeight = 1334f;

    private static Font font;

    public static Font Font
    {
        get
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }

    public static Canvas CreateCanvas(string name)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
        scaler.matchWidthOrHeight = 0f; // 세로 고정이라 가로 폭 기준

        EnsureEventSystem();
        return canvas;
    }

    // UI 입력에 필요. 프로젝트 입력 설정이 Input System 전용이라 InputSystemUIInputModule을 쓴다.
    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    // 왼쪽 위 기준 (x, y)에 w×h 크기로 놓는다.
    public static T Place<T>(T graphic, float x, float y, float w, float h) where T : Component
    {
        var rt = (RectTransform)graphic.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return graphic;
    }

    public static void Stretch(Component graphic, float padding = 0f)
    {
        var rt = (RectTransform)graphic.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding, 0f);
        rt.offsetMax = new Vector2(-padding, 0f);
    }

    public static Image CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static Text CreateText(Transform parent, string name, int fontSize, TextAnchor alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = Font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    public static InputField CreateInputField(Transform parent, string name, string placeholder, int fontSize)
    {
        var background = CreateImage(parent, name, new Color(1f, 1f, 1f, 0.95f));
        background.raycastTarget = true;

        var text = CreateText(background.transform, "Text", fontSize, TextAnchor.MiddleLeft);
        text.color = Color.black;
        text.supportRichText = false;
        Stretch(text, 12f);

        var hint = CreateText(background.transform, "Placeholder", fontSize, TextAnchor.MiddleLeft);
        hint.text = placeholder;
        hint.color = new Color(0f, 0f, 0f, 0.4f);
        hint.fontStyle = FontStyle.Italic;
        Stretch(hint, 12f);

        var input = background.gameObject.AddComponent<InputField>();
        input.targetGraphic = background;
        input.textComponent = text;
        input.placeholder = hint;
        input.lineType = InputField.LineType.SingleLine;
        return input;
    }

    public static Button CreateButton(Transform parent, string name, string label, int fontSize)
    {
        var background = CreateImage(parent, name, new Color(0.2f, 0.45f, 0.9f, 1f));
        background.raycastTarget = true;

        var text = CreateText(background.transform, "Label", fontSize, TextAnchor.MiddleCenter);
        text.text = label;
        Stretch(text);

        var button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        return button;
    }
}
