#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

public static class MediaPickerSceneSetup
{
    [MenuItem("Tools/Setup Media Picker Scene")]
    public static void SetupScene()
    {
        // 1. Ensure EventSystem exists
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
        }

        // 2. Create or find Canvas
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        GameObject canvasGo;
        if (canvas == null)
        {
            canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
        }
        else
        {
            canvasGo = canvas.gameObject;
        }

        // 3. Find or Create MediaPickerManager
        MediaPickerManager manager = Object.FindFirstObjectByType<MediaPickerManager>();
        GameObject managerGo;
        if (manager == null)
        {
            managerGo = new GameObject("MediaPickerManager", typeof(MediaPickerManager));
            manager = managerGo.GetComponent<MediaPickerManager>();
            Undo.RegisterCreatedObjectUndo(managerGo, "Create MediaPickerManager");
        }
        else
        {
            managerGo = manager.gameObject;
        }

        // 4. Preview Container
        Transform existingPreview = canvasGo.transform.Find("PreviewContainer");
        GameObject previewContainer;
        if (existingPreview != null)
        {
            previewContainer = existingPreview.gameObject;
        }
        else
        {
            previewContainer = new GameObject("PreviewContainer", typeof(RectTransform));
            previewContainer.transform.SetParent(canvasGo.transform, false);

            RectTransform previewRect = previewContainer.GetComponent<RectTransform>();
            previewRect.anchorMin = new Vector2(0.05f, 0.48f);
            previewRect.anchorMax = new Vector2(0.95f, 0.92f);
            previewRect.offsetMin = Vector2.zero;
            previewRect.offsetMax = Vector2.zero;
        }

        // Raw Image for preview
        Transform existingRaw = previewContainer.transform.Find("RawImagePreview");
        RawImage rawImage;
        if (existingRaw != null)
        {
            rawImage = existingRaw.GetComponent<RawImage>();
        }
        else
        {
            GameObject rawGo = new GameObject("RawImagePreview", typeof(RectTransform), typeof(RawImage));
            rawGo.transform.SetParent(previewContainer.transform, false);
            RectTransform r = rawGo.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;

            rawImage = rawGo.GetComponent<RawImage>();
            rawImage.color = Color.white;
        }

        // Video Player attached to manager
        VideoPlayer videoPlayer = managerGo.GetComponent<VideoPlayer>();
        if (videoPlayer == null)
        {
            videoPlayer = managerGo.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.source = VideoSource.Url;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        }

        // 5. On-Screen Debug 1: Original Size Text
        Transform existingOrigSize = canvasGo.transform.Find("OriginalSizeText");
        Text origSizeText;
        if (existingOrigSize != null)
        {
            origSizeText = existingOrigSize.GetComponent<Text>();
        }
        else
        {
            GameObject origGo = new GameObject("OriginalSizeText", typeof(RectTransform), typeof(Text));
            origGo.transform.SetParent(canvasGo.transform, false);

            RectTransform origRect = origGo.GetComponent<RectTransform>();
            origRect.anchorMin = new Vector2(0.05f, 0.43f);
            origRect.anchorMax = new Vector2(0.95f, 0.47f);
            origRect.offsetMin = Vector2.zero;
            origRect.offsetMax = Vector2.zero;

            origSizeText = origGo.GetComponent<Text>();
            origSizeText.text = "Original: --";
            origSizeText.alignment = TextAnchor.MiddleCenter;
            origSizeText.fontSize = 28;
            origSizeText.fontStyle = FontStyle.Bold;
            origSizeText.color = new Color(0.9f, 0.9f, 0.3f);
        }

        // 6. On-Screen Debug 2: Compressed Size Text
        Transform existingCompSize = canvasGo.transform.Find("CompressedSizeText");
        Text compSizeText;
        if (existingCompSize != null)
        {
            compSizeText = existingCompSize.GetComponent<Text>();
        }
        else
        {
            GameObject compGo = new GameObject("CompressedSizeText", typeof(RectTransform), typeof(Text));
            compGo.transform.SetParent(canvasGo.transform, false);

            RectTransform compRect = compGo.GetComponent<RectTransform>();
            compRect.anchorMin = new Vector2(0.05f, 0.38f);
            compRect.anchorMax = new Vector2(0.95f, 0.42f);
            compRect.offsetMin = Vector2.zero;
            compRect.offsetMax = Vector2.zero;

            compSizeText = compGo.GetComponent<Text>();
            compSizeText.text = "Compressed: --";
            compSizeText.alignment = TextAnchor.MiddleCenter;
            compSizeText.fontSize = 28;
            compSizeText.fontStyle = FontStyle.Bold;
            compSizeText.color = new Color(0.2f, 0.95f, 0.45f);
        }

        // 7. Status Text
        Transform existingStatus = canvasGo.transform.Find("StatusText");
        Text statusText;
        if (existingStatus != null)
        {
            statusText = existingStatus.GetComponent<Text>();
        }
        else
        {
            GameObject statusGo = new GameObject("StatusText", typeof(RectTransform), typeof(Text));
            statusGo.transform.SetParent(canvasGo.transform, false);

            RectTransform statusRect = statusGo.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.05f, 0.33f);
            statusRect.anchorMax = new Vector2(0.95f, 0.37f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;

            statusText = statusGo.GetComponent<Text>();
            statusText.text = "Select or Capture media to begin";
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.fontSize = 22;
            statusText.color = Color.white;
        }

        // 8. Toggle Comparison Button (Switch between Original and Compressed on screen)
        Transform existingToggleBtn = canvasGo.transform.Find("Btn_TogglePreview");
        Button toggleBtn;
        if (existingToggleBtn != null)
        {
            toggleBtn = existingToggleBtn.GetComponent<Button>();
        }
        else
        {
            GameObject toggleGo = new GameObject("Btn_TogglePreview", typeof(RectTransform), typeof(Image), typeof(Button));
            toggleGo.transform.SetParent(canvasGo.transform, false);

            RectTransform toggleRect = toggleGo.GetComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0.12f, 0.25f);
            toggleRect.anchorMax = new Vector2(0.88f, 0.31f);
            toggleRect.offsetMin = Vector2.zero;
            toggleRect.offsetMax = Vector2.zero;

            Image img = toggleGo.GetComponent<Image>();
            img.color = new Color(0.6f, 0.3f, 0.9f); // Purple

            toggleBtn = toggleGo.GetComponent<Button>();

            GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(toggleGo.transform, false);
            RectTransform tr = textGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;

            Text txt = textGo.GetComponent<Text>();
            txt.text = "👀 Switch to Original";
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontSize = 28;
            txt.fontStyle = FontStyle.Bold;
            txt.color = Color.white;
        }

        // Clean up old manual compress button if present
        Transform oldCompBtn = canvasGo.transform.Find("Btn_CompressMedia");
        if (oldCompBtn != null)
        {
            Object.DestroyImmediate(oldCompBtn.gameObject);
        }

        // 9. 4 Media Buttons (Vertical Layout)
        Transform existingButtons = canvasGo.transform.Find("ButtonGroup");
        GameObject buttonGroup;
        if (existingButtons != null)
        {
            buttonGroup = existingButtons.gameObject;
        }
        else
        {
            buttonGroup = new GameObject("ButtonGroup", typeof(RectTransform), typeof(VerticalLayoutGroup));
            buttonGroup.transform.SetParent(canvasGo.transform, false);

            RectTransform groupRect = buttonGroup.GetComponent<RectTransform>();
            groupRect.anchorMin = new Vector2(0.08f, 0.02f);
            groupRect.anchorMax = new Vector2(0.92f, 0.23f);
            groupRect.offsetMin = Vector2.zero;
            groupRect.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout = buttonGroup.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 14;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
        }

        CreateButton(buttonGroup.transform, "Btn_PickImage", "🖼️ Pick Image (Gallery)", manager.OnPickImageClicked, new Color(0.2f, 0.55f, 0.85f));
        CreateButton(buttonGroup.transform, "Btn_PickVideo", "🎬 Pick Video (Gallery)", manager.OnPickVideoClicked, new Color(0.15f, 0.45f, 0.75f));
        CreateButton(buttonGroup.transform, "Btn_CaptureImage", "📷 Capture Photo (Camera)", manager.OnCaptureImageClicked, new Color(0.25f, 0.68f, 0.4f));
        CreateButton(buttonGroup.transform, "Btn_RecordVideo", "🎥 Record Video (Camera)", manager.OnRecordVideoClicked, new Color(0.8f, 0.3f, 0.3f));

        // 10. Wire references
        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("previewImage").objectReferenceValue = rawImage;
        so.FindProperty("videoPlayer").objectReferenceValue = videoPlayer;
        so.FindProperty("statusText").objectReferenceValue = statusText;
        so.FindProperty("originalSizeText").objectReferenceValue = origSizeText;
        so.FindProperty("compressedSizeText").objectReferenceValue = compSizeText;
        so.FindProperty("togglePreviewButton").objectReferenceValue = toggleBtn;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("✅ Scene Updated with Auto-Compress & Comparison Setup!");
    }

    private static void CreateButton(Transform parent, string name, string labelText, UnityEngine.Events.UnityAction action, Color btnColor)
    {
        Transform existing = parent.Find(name);
        GameObject btnGo;
        if (existing != null)
        {
            btnGo = existing.gameObject;
        }
        else
        {
            btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(parent, false);

            Image img = btnGo.GetComponent<Image>();
            img.color = btnColor;

            Button btn = btnGo.GetComponent<Button>();
            UnityEventTools.AddPersistentListener(btn.onClick, action);

            GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(btnGo.transform, false);

            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text text = textGo.GetComponent<Text>();
            text.text = labelText;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 28;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
        }
    }
}
#endif
