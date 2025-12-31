#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class UILayoutFixer : EditorWindow
{
    [MenuItem("Tools/Fix Login UI Layout")]
    public static void FixLayout()
    {
        GameObject canvasObj = GameObject.Find("LoginCanvas");
        if (canvasObj == null) { Debug.LogError("LoginCanvas not found!"); return; }

        // 0. Canvas & Scaler Setup (Standard 1920x1080)
        Canvas canvas = canvasObj.GetComponent<Canvas>();
        if (canvas != null) canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // 1. Background Image (Full Stretch)
        GameObject bgObj = GameObject.Find("BackgroundImage");
        if (bgObj != null)
        {
            RectTransform rt = bgObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            
            Image img = bgObj.GetComponent<Image>();
            if (img != null) img.color = Color.white;
        }

        // 2. Input Fields Setup
        GameObject userObj = GameObject.Find("UsernameInputField");
        GameObject passObj = GetOrCreateChild(canvasObj, "PasswordInputField");
        
        // Username Input
        if (userObj != null)
        {
            SetupInputField(userObj, new Vector2(0, 110), "Enter Username...", false);
        }

        // Password Input
        SetupInputField(passObj, new Vector2(0, 20), "Enter Password...", true);

        // 3. Login Button (Centered, 400x60)
        GameObject btnObj = GameObject.Find("LoginButton");
        if (btnObj != null)
        {
            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, -90);
            rt.sizeDelta = new Vector2(400, 60);
            rt.localScale = Vector3.one;

            GameObject btnTextObj = GetOrCreateChild(btnObj, "ButtonText");
            Text btnText = btnTextObj.GetComponent<Text>();
            if (btnText == null) btnText = btnTextObj.AddComponent<Text>();
            SetupText(btnText, "LOGIN", Color.black);
            FillStretch(btnTextObj.GetComponent<RectTransform>(), Vector4.zero);
        }

        // 4. Update References in Controller
        GameObject controllerObj = GameObject.Find("LoginController");
        if (controllerObj != null)
        {
            LoginUI ui = controllerObj.GetComponent<LoginUI>();
            if (ui != null)
            {
                ui.backgroundImage = bgObj?.GetComponent<Image>();
                ui.usernameInput = userObj?.GetComponent<InputField>();
                ui.passwordInput = passObj?.GetComponent<InputField>();
                ui.loginButton = btnObj?.GetComponent<Button>();
                ui.mainCanvasGroup = canvasObj.GetComponent<CanvasGroup>();
                if (ui.mainCanvasGroup == null) ui.mainCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
            }
        }

        EditorUtility.SetDirty(canvasObj);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvasObj.scene);
        Debug.Log("Login UI Layout Precision Fixed with Password Field (Complete Script)!");
    }

    private static void SetupInputField(GameObject go, Vector2 pos, string placeholder, bool isPassword)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(400, 60);
        rt.localScale = Vector3.one;

        InputField inputField = go.GetComponent<InputField>();
        if (inputField == null) inputField = go.AddComponent<InputField>();
        inputField.contentType = isPassword ? InputField.ContentType.Password : InputField.ContentType.Standard;

        // Character Text setup
        GameObject textObj = GetOrCreateChild(go, "Text");
        Text inputText = textObj.GetComponent<Text>();
        if (inputText == null) inputText = textObj.AddComponent<Text>();
        SetupText(inputText, "", Color.black);
        FillStretch(textObj.GetComponent<RectTransform>(), new Vector4(10, 5, 10, 5));

        // Placeholder setup
        GameObject placeholderObj = GetOrCreateChild(go, "Placeholder");
        Text placeholderText = placeholderObj.GetComponent<Text>();
        if (placeholderText == null) placeholderText = placeholderObj.AddComponent<Text>();
        SetupText(placeholderText, placeholder, new Color(0.2f, 0.2f, 0.2f, 0.5f));
        placeholderText.fontStyle = FontStyle.Italic;
        FillStretch(placeholderObj.GetComponent<RectTransform>(), new Vector4(10, 5, 10, 5));

        inputField.textComponent = inputText;
        inputField.placeholder = placeholderText;

        if (go.GetComponent<Image>() == null)
        {
            Image img = go.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.8f);
        }
    }

    private static GameObject GetOrCreateChild(GameObject parent, string name)
    {
        Transform child = parent.transform.Find(name);
        if (child != null) return child.gameObject;
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static void SetupText(Text t, string content, Color color)
    {
        t.text = content;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
    }

    private static void FillStretch(RectTransform rt, Vector4 margins)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(margins.x, margins.y);
        rt.offsetMax = new Vector2(-margins.z, -margins.w);
        rt.localScale = Vector3.one;
    }
}
#endif
