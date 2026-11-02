using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

[DisallowMultipleComponent]
public sealed class WorldPhoneMessages : MonoBehaviour
{
    private sealed class ChatMessage
    {
        public readonly string Text;
        public readonly bool IsPlayer;

        public ChatMessage(string text, bool isPlayer)
        {
            Text = text;
            IsPlayer = isPlayer;
        }
    }

    private readonly List<ChatMessage> messages = new List<ChatMessage>();
    private InputAction togglePhoneAction;
    private bool isOpen;
    private bool hasStartedConversation;
    private bool replyPending;
    private bool sceneConversation;
    private bool sceneReplyArrived;
    private bool sceneReplySent;
    private float replyTime;
    private string clickableLinkText;
    private bool linkClicked;
    private int pendingImageNumber;
    private bool imageOverlayVisible;
        private bool linkLoading;
    private Texture2D imageViewerTexture;
    private string imageViewerMissingReference;
    private int visibleMessageCount;
    private int lastDrawnVisibleMessageCount;
    private Vector2 messageScroll;
    private float nextMessageRevealTime;
    private bool sequentialConversation;
    private GUIStyle headerStyle;
    private GUIStyle detailStyle;
    private GUIStyle incomingStyle;
    private GUIStyle outgoingStyle;
    private GUIStyle hintStyle;
    private Canvas phoneScreenCanvas;
    private RectTransform phoneScreenRoot;
    private string lastScreenState;

    private sealed class PhoneMessageView
    {
        public readonly string Text;
        public readonly bool IsPlayer;
        public PhoneMessageView(string text, bool isPlayer) { Text = text; IsPlayer = isPlayer; }
    }

    public void AttachPhoneScreen(Transform phoneRoot)
    {
        if (phoneRoot == null) return;

        var existingCanvases = phoneRoot.GetComponentsInChildren<Canvas>(true);
        foreach (var existingCanvas in existingCanvases)
            if (existingCanvas.name == "PhoneScreenCanvas") Destroy(existingCanvas.gameObject);

        Transform screenSurface = null;
        Mesh screenMesh = null;
        var renderers = phoneRoot.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var meshRenderer in renderers)
        {
            bool isScreen = false;
            foreach (var material in meshRenderer.sharedMaterials)
                if (material != null && material.name.IndexOf("Screen", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    isScreen = true;
            if (!isScreen) continue;
            var meshFilter = meshRenderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null) continue;
            screenSurface = meshRenderer.transform;
            screenMesh = meshFilter.sharedMesh;
            break;
        }

        if (screenSurface == null || screenMesh == null)
        {
            Debug.LogError("WorldPhoneMessages could not find the GLB's Screen mesh; phone UI was not placed as a floating panel.", this);
            return;
        }

        var screenBounds = screenMesh.bounds;
        int thinAxis = screenBounds.size.x < screenBounds.size.y && screenBounds.size.x < screenBounds.size.z ? 0 :
                       screenBounds.size.y < screenBounds.size.z ? 1 : 2;
        Vector3 screenNormal;
        float screenWidth;
        float screenHeight;
        Vector3 screenPosition = screenBounds.center;
        Quaternion screenRotation;
        const float edgeMargin = 0.90f;
        if (thinAxis == 1)
        {
            screenNormal = Vector3.up;
            screenPosition.y = screenBounds.max.y + Mathf.Max(0.0005f, screenBounds.size.y * 0.5f + 0.0002f);
            screenWidth = screenBounds.size.x * edgeMargin;
            screenHeight = screenBounds.size.z * 0.84f;
            // GLB's portrait axis points toward local -Z; this faces the UI out of the display.
            screenRotation = Quaternion.Euler(-90f, 0f, 0f);
        }
        else if (thinAxis == 0)
        {
            screenNormal = Vector3.right;
            screenPosition.x = screenBounds.max.x + Mathf.Max(0.0005f, screenBounds.size.x * 0.5f + 0.0002f);
            screenWidth = screenBounds.size.z * edgeMargin;
            screenHeight = screenBounds.size.y * 0.84f;
            screenRotation = Quaternion.LookRotation(screenNormal, Vector3.up);
        }
        else
        {
            screenNormal = Vector3.forward;
            screenPosition.z = screenBounds.max.z + Mathf.Max(0.0005f, screenBounds.size.z * 0.5f + 0.0002f);
            screenWidth = screenBounds.size.x * edgeMargin;
            screenHeight = screenBounds.size.y * 0.84f;
            screenRotation = Quaternion.identity;
        }

        var screenObject = new GameObject("PhoneScreenCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        screenObject.transform.SetParent(screenSurface, false);
        screenObject.transform.localPosition = screenPosition + screenNormal * 0.0002f;
        screenObject.transform.localRotation = screenRotation;
        screenObject.transform.localScale = Vector3.one;
        phoneScreenCanvas = screenObject.GetComponent<Canvas>();
        phoneScreenCanvas.renderMode = RenderMode.WorldSpace;
        phoneScreenCanvas.sortingOrder = 20;
        var scaler = screenObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = screenWidth / 220f;
        scaler.referencePixelsPerUnit = 100f;

        phoneScreenRoot = screenObject.GetComponent<RectTransform>();
        phoneScreenRoot.sizeDelta = new Vector2(220f, 220f * screenHeight / Mathf.Max(0.001f, screenWidth));
        EnsurePhoneUiEventSystem();
        RefreshPhoneScreen(true);
    }

    private static void EnsurePhoneUiEventSystem()
    {
        if (EventSystem.current != null) return;
        var eventSystemObject = new GameObject("Phone UI EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystemObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    private void RefreshPhoneScreen(bool force = false)
    {
        if (phoneScreenRoot == null) return;
        phoneScreenRoot.gameObject.SetActive(isOpen);
        if (!isOpen) return;

        string state = visibleMessageCount + ":" + replyPending + ":" + imageOverlayVisible + ":" + linkLoading + ":" +
                       (imageViewerTexture != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(imageViewerTexture) : 0) + ":" + clickableLinkText + ":" + linkClicked;
        for (int i = 0; i < visibleMessageCount && i < messages.Count; i++) state += "|" + messages[i].Text + messages[i].IsPlayer;
        if (!force && state == lastScreenState) return;
        lastScreenState = state;

        for (int i = phoneScreenRoot.childCount - 1; i >= 0; i--) Destroy(phoneScreenRoot.GetChild(i).gameObject);
        var background = MakePhonePanel(phoneScreenRoot, "Display", new Color(0.035f, 0.05f, 0.075f, 1f));
        Stretch(background.rectTransform, 0, 0, 0, 0);

        var header = MakePhonePanel(phoneScreenRoot, "Header", new Color(0.08f, 0.12f, 0.18f, 1f));
        SetTop(header.rectTransform, 0, 0, 0, 68);
        AddPhoneText(header.transform, "MESSAGES", 18, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft, 14, 4, 14, 28);
        AddPhoneText(header.transform, "FRED  •  ONLINE", 11, FontStyle.Normal, new Color(.68f,.79f,.9f), TextAnchor.MiddleLeft, 14, 34, 14, 20);

        if (imageOverlayVisible)
        {
            var imageArea = MakePhonePanel(phoneScreenRoot, "Image Viewer", Color.black);
            SetStretchMargins(imageArea.rectTransform, 10, 78, 10, 46);
            if (imageViewerTexture != null)
            {
                var imageObject = new GameObject("Message Image", typeof(RectTransform), typeof(RawImage));
                imageObject.transform.SetParent(imageArea.transform, false);
                var raw = imageObject.GetComponent<RawImage>(); raw.texture = imageViewerTexture; raw.uvRect = new Rect(0, 0, 1, 1);
                var imageRect = raw.rectTransform; Stretch(imageRect, 8, 8, 8, 8);
                raw.raycastTarget = false;
            }
            else AddPhoneText(imageArea.transform, "IMAGE UNAVAILABLE\n" + imageViewerMissingReference, 12, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, 8, 8, 8, 8);
        }
        else
        {
            float y = -82f;
            for (int i = 0; i < visibleMessageCount && i < messages.Count; i++)
            {
                var message = messages[i];
                bool isLink = !string.IsNullOrEmpty(clickableLinkText) && message.Text == clickableLinkText && !linkClicked;
                float bubbleHeight = Mathf.Clamp(42f + Mathf.Ceil(message.Text.Length / 22f) * 18f, 54f, 230f);
                float bubbleWidth = 190f;
                var bubble = MakePhonePanel(phoneScreenRoot, "Message " + i, message.IsPlayer ? new Color(.07f,.32f,.62f,1f) : new Color(.84f,.89f,.94f,1f));
                var rt = bubble.rectTransform;
                rt.anchorMin = new Vector2(message.IsPlayer ? 1f : 0f, 1f); rt.anchorMax = rt.anchorMin;
                rt.pivot = new Vector2(message.IsPlayer ? 1f : 0f, 1f);
                rt.anchoredPosition = new Vector2(message.IsPlayer ? -10f : 10f, y);
                rt.sizeDelta = new Vector2(bubbleWidth, bubbleHeight);
                Color textColor = message.IsPlayer ? Color.white : new Color(.04f,.07f,.1f);
                var label = AddPhoneText(bubble.transform, message.Text, 14, FontStyle.Normal, textColor, TextAnchor.MiddleLeft, 12, 5, 12, 5);
                if (isLink)
                {
                    var button = bubble.gameObject.AddComponent<Button>();
                    button.targetGraphic = bubble;
                    button.onClick.AddListener(() => { linkClicked = true; pendingImageNumber = 8; imageOverlayVisible = true; lastScreenState = null; RefreshPhoneScreen(true); });
                }
                label.raycastTarget = false;
                y -= bubbleHeight + 12f;
            }
            if (replyPending)
            {
                var typing = MakePhonePanel(phoneScreenRoot, "Typing", new Color(.84f,.89f,.94f,1f));
                typing.rectTransform.anchorMin = typing.rectTransform.anchorMax = new Vector2(0,1);
                typing.rectTransform.pivot = new Vector2(0,1); typing.rectTransform.anchoredPosition = new Vector2(10,y);
                typing.rectTransform.sizeDelta = new Vector2(92,42);
                AddPhoneText(typing.transform,"typing...",12,FontStyle.Normal,new Color(.1f,.14f,.2f),TextAnchor.MiddleCenter,4,4,4,4);
            }
        }
        var footer = AddPhoneText(phoneScreenRoot, linkLoading ? "LOADING..." : imageOverlayVisible ? "UP ARROW  •  CLOSE PHONE" : sequentialConversation && !CustomConversationComplete ? "MESSAGES..." : sceneConversation ? (sceneReplySent ? "UP ARROW  •  CLOSE PHONE" : "ENTER  •  SEND NEVER") : "UP ARROW  •  CLOSE PHONE", 10, FontStyle.Bold, new Color(.55f,.68f,.8f), TextAnchor.MiddleCenter, 8, 8, 8, 8);
        footer.rectTransform.anchorMin = new Vector2(0, 0); footer.rectTransform.anchorMax = new Vector2(1, 0);
        footer.rectTransform.pivot = new Vector2(.5f, 0); footer.rectTransform.anchoredPosition = new Vector2(0, 9);
        footer.rectTransform.sizeDelta = new Vector2(-16, 24);
    }

    private static Image MakePhonePanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>(); image.color = color; return image;
    }

    private static Text AddPhoneText(Transform parent, string value, int size, FontStyle style, Color color, TextAnchor alignment, float left, float top, float right, float bottom, float height = 0)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>(); text.text = value; text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size; text.fontStyle = style; text.color = color; text.alignment = alignment; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = true;
        var rt = text.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
        if (height > 0) SetTop(rt, left, -top, right, height);
        return text;
    }

    private static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
    { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(left,bottom); rt.offsetMax = new Vector2(-right,-top); }
    private static void SetStretchMargins(RectTransform rt, float left, float top, float right, float bottom)
    { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(left,bottom); rt.offsetMax = new Vector2(-right,-top); }
    private static void SetTop(RectTransform rt, float left, float top, float right, float height)
    { rt.anchorMin = new Vector2(0,1); rt.anchorMax = new Vector2(1,1); rt.pivot = new Vector2(.5f,1); rt.anchoredPosition = new Vector2((left-right)*.5f,top); rt.sizeDelta = new Vector2(-left-right,height); }

    private void Awake()
    {
        togglePhoneAction = new InputAction(
            "Toggle Phone",
            InputActionType.Button,
            "<Keyboard>/upArrow");
    }

    private void OnEnable()
    {
        if (togglePhoneAction != null)
            togglePhoneAction.Enable();
    }

    private void OnDisable()
    {
        if (togglePhoneAction != null)
            togglePhoneAction.Disable();
    }

    private void OnDestroy()
    {
        if (togglePhoneAction != null)
            togglePhoneAction.Dispose();
    }

    private void Update()
    {
        if (!sceneConversation &&
            togglePhoneAction.WasPressedThisFrame() &&
            Cursor.lockState == CursorLockMode.Locked &&
            Time.timeScale > 0f)
        {
            isOpen = !isOpen;
            if (isOpen)
                OpenConversation();
        }

        if (replyPending && Time.unscaledTime >= replyTime)
        {
            messages.Add(new ChatMessage(
                sceneConversation ? "I believe you open it" : "I'm nearby. What's going on?",
                !sceneConversation));
            replyPending = false;
            if (sceneConversation)
            {
                sceneReplyArrived = true;

                // The scene conversation is scripted, so the player never has to press anything:
                // send the "NEVER" reply automatically as soon as Fred's message lands.
                TrySendSceneReply();
            }

            if (!sequentialConversation)
                visibleMessageCount = messages.Count;
        }

        if (isOpen && sequentialConversation && visibleMessageCount < messages.Count &&
            Time.unscaledTime >= nextMessageRevealTime)
        {
            visibleMessageCount++;
            nextMessageRevealTime = Time.unscaledTime + 1.15f;
        }

        if (sceneConversation && isOpen && Keyboard.current != null &&
            Keyboard.current.enterKey.wasPressedThisFrame)
        {
            TrySendSceneReply();
        }

        RefreshPhoneScreen();
    }

    public bool SceneReplySent => sceneReplySent;
    public bool IsPhoneOpen => isOpen;
    public bool CustomConversationComplete => !sequentialConversation || visibleMessageCount >= messages.Count;
    public bool LinkClicked => linkClicked;
    public int PendingImageNumber => pendingImageNumber;
    public bool HasImageOverlay => imageOverlayVisible;

    public void ConfigureSceneConversation()
    {
        sceneConversation = true;
        isOpen = false;
        hasStartedConversation = false;
        replyPending = false;
        sceneReplyArrived = false;
        sceneReplySent = false;
        clickableLinkText = null;
        linkClicked = false;
        pendingImageNumber = 0;
        imageOverlayVisible = false;
        linkLoading = false;
        imageViewerTexture = null;
        imageViewerMissingReference = null;
        visibleMessageCount = 0;
        sequentialConversation = false;
        messages.Clear();
        ResetMessageScroll();
    }

    public void OpenCustomConversation(List<string> conversationText, List<bool> playerFlags, string clickTarget = null)
    {
        if (conversationText == null || conversationText.Count == 0)
        {
            return;
        }

        messages.Clear();
        ResetMessageScroll();
        sceneConversation = true;
        isOpen = true;
        hasStartedConversation = true;
        replyPending = false;
        sceneReplyArrived = false;
        sceneReplySent = true;
        clickableLinkText = clickTarget;
        linkClicked = false;
        pendingImageNumber = 0;
        imageOverlayVisible = false;
        linkLoading = false;
        imageViewerTexture = null;
        imageViewerMissingReference = null;
        visibleMessageCount = conversationText.Count > 0 ? 1 : 0;
        nextMessageRevealTime = Time.unscaledTime + 1.15f;
        sequentialConversation = true;

        for (int i = 0; i < conversationText.Count; i++)
        {
            bool isPlayer = i < playerFlags.Count && playerFlags[i];
            messages.Add(new ChatMessage(conversationText[i], isPlayer));
        }
    }

    public void ClosePhone()
    {
        isOpen = false;
        imageOverlayVisible = false;
        linkLoading = false;
        imageViewerTexture = null;
        imageViewerMissingReference = null;
    }

    public void ShowLoadingScreen(string loadingText)
    {
        messages.Clear();
        ResetMessageScroll();
        sceneConversation = true;
        isOpen = true;
        hasStartedConversation = true;
        replyPending = false;
        sceneReplyArrived = false;
        sceneReplySent = true;
        sequentialConversation = false;
        visibleMessageCount = 1;
        clickableLinkText = null;
        linkClicked = false;
        pendingImageNumber = 0;
        imageOverlayVisible = false;
        linkLoading = true;
        imageViewerTexture = null;
        imageViewerMissingReference = null;
        messages.Add(new ChatMessage(loadingText, false));
    }

    public void OpenImageViewer(Texture2D image, string missingReference)
    {
        messages.Clear();
        ResetMessageScroll();
        isOpen = true;
        sceneConversation = true;
        hasStartedConversation = true;
        replyPending = false;
        sceneReplyArrived = false;
        sceneReplySent = true;
        sequentialConversation = false;
        visibleMessageCount = 0;
        clickableLinkText = null;
        linkClicked = false;
        linkLoading = false;
        imageViewerTexture = image;
        imageViewerMissingReference = image == null ? missingReference : null;
        imageOverlayVisible = true;
    }

    public void OpenSceneConversation()
    {
        if (!sceneConversation)
            ConfigureSceneConversation();

        isOpen = true;
        if (hasStartedConversation)
            return;

        hasStartedConversation = true;
        messages.Add(new ChatMessage("XYZ.ONION", false));
        visibleMessageCount = messages.Count;
        replyPending = true;
        replyTime = Time.unscaledTime + 1f;
    }

    public void CloseSceneConversation()
    {
        ClosePhone();
    }

    private void TrySendSceneReply()
    {
        if (!sceneReplyArrived || sceneReplySent)
            return;

        messages.Add(new ChatMessage("NEVER", true));
        sceneReplySent = true;

        // Make the outgoing reply appear immediately instead of waiting for the reveal timer.
        visibleMessageCount = messages.Count;
    }

    private void OpenConversation()
    {
        if (hasStartedConversation)
            return;

        hasStartedConversation = true;
        messages.Add(new ChatMessage("Hey, are you close? I need to talk to you.", false));
        visibleMessageCount = messages.Count;
        replyPending = true;
        replyTime = Time.unscaledTime + 1.25f;
    }

    private void OnGUI()
    {
        if (!isOpen || phoneScreenCanvas != null)
            return;

        EnsureStyles();

        Color oldColor = GUI.color;
        Color oldContentColor = GUI.contentColor;
        GUI.color = Color.white;
        GUI.contentColor = Color.white;
        float width = Mathf.Min(250f, Screen.width * 0.30f);
        float height = Mathf.Min(420f, Screen.height - 40f);
        Rect phone = new Rect(
            Screen.width - width - 20f,
            (Screen.height - height) * 0.5f,
            width,
            height);

        // Slim black rails frame the screen like a modern handset, with a small
        // camera notch centered above the content.
        Rect deviceFrame = new Rect(phone.x - 6f, phone.y - 6f, phone.width + 12f, phone.height + 12f);
        DrawPanel(deviceFrame, new Color(0.015f, 0.02f, 0.03f, 1f));
        DrawPanel(phone, new Color(0.1f, 0.12f, 0.15f, 0.98f));
        Rect notch = new Rect(phone.center.x - 38f, phone.y + 3f, 76f, 9f);
        DrawPanel(notch, new Color(0.015f, 0.02f, 0.03f, 1f));
        Rect cameraLens = new Rect(notch.x + 10f, notch.y + 2.5f, 4f, 4f);
        DrawPanel(cameraLens, new Color(0.15f, 0.34f, 0.52f, 1f));

        Rect header = new Rect(phone.x + 12f, phone.y + 18f, phone.width - 24f, 62f);
        DrawPanel(header, new Color(0.16f, 0.2f, 0.25f, 1f));
        GUI.Label(new Rect(header.x + 14f, header.y + 7f, header.width - 28f, 25f), "MESSAGES", headerStyle);
        GUI.Label(new Rect(header.x + 14f, header.y + 32f, header.width - 28f, 20f), "FRED  \u2022  ONLINE", detailStyle);

        float contentX = phone.x + 16f;
        float contentWidth = phone.width - 32f;
        float y = header.yMax + 18f;
        float bottom = phone.yMax - 40f;
        Rect messageViewport = new Rect(contentX, y, contentWidth, Mathf.Max(80f, bottom - y));

        if (imageOverlayVisible)
        {
            Rect imageRect = messageViewport;
            DrawPanel(imageRect, Color.black);
            if (imageViewerTexture != null)
            {
                GUI.DrawTexture(imageRect, imageViewerTexture, ScaleMode.ScaleToFit, false);
            }
            else
            {
                GUIStyle missingStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };
                missingStyle.normal.textColor = Color.white;
                GUI.Label(imageRect, "IMAGE UNAVAILABLE\n" + imageViewerMissingReference, missingStyle);
            }
        }

        if (!imageOverlayVisible)
        {
            float fullHeight = 0f;
            for (int i = 0; i < visibleMessageCount; i++)
            {
                ChatMessage message = messages[i];
                float bubbleWidth = contentWidth * (message.IsPlayer ? 0.82f : 0.88f);
                GUIStyle style = message.IsPlayer ? outgoingStyle : incomingStyle;
                fullHeight += Mathf.Max(48f, style.CalcHeight(new GUIContent(message.Text), bubbleWidth - 24f) + 24f) + 12f;
            }

            if (replyPending)
                fullHeight += 50f;

            float viewportHeight = messageViewport.height;
            float contentHeight = Mathf.Max(viewportHeight, fullHeight);
            if (visibleMessageCount > lastDrawnVisibleMessageCount)
            {
                messageScroll.y = Mathf.Max(0f, contentHeight - viewportHeight);
                lastDrawnVisibleMessageCount = visibleMessageCount;
            }

            Rect contentRect = new Rect(0f, 0f, contentWidth, contentHeight);
            messageScroll = GUI.BeginScrollView(messageViewport, messageScroll, contentRect, false, true);
            float messageY = 0f;
            for (int i = 0; i < visibleMessageCount; i++)
            {
                ChatMessage message = messages[i];
                float bubbleWidth = contentWidth * (message.IsPlayer ? 0.82f : 0.88f);
                GUIStyle style = message.IsPlayer ? outgoingStyle : incomingStyle;
                float bubbleHeight = Mathf.Max(48f, style.CalcHeight(new GUIContent(message.Text), bubbleWidth - 24f) + 24f);
                Rect bubble = new Rect(
                    message.IsPlayer ? contentWidth - bubbleWidth : 0f,
                    messageY,
                    bubbleWidth,
                    bubbleHeight);

                DrawPanel(bubble, message.IsPlayer
                    ? new Color(0.12f, 0.38f, 0.67f, 1f)
                    : new Color(0.88f, 0.92f, 0.96f, 1f));

                Rect textRect = new Rect(bubble.x + 12f, bubble.y + 10f, bubble.width - 24f, bubble.height - 20f);
                if (!string.IsNullOrEmpty(clickableLinkText) &&
                    string.Equals(message.Text, clickableLinkText, System.StringComparison.Ordinal) &&
                    !linkClicked)
                {
                    if (GUI.Button(textRect, message.Text, style))
                    {
                        linkClicked = true;
                        pendingImageNumber = 8;
                        imageOverlayVisible = true;
                    }
                }
                else
                {
                    GUI.Label(textRect, message.Text, style);
                }

                messageY = bubble.yMax + 12f;
            }

            if (replyPending)
            {
                Rect typingBubble = new Rect(0f, messageY, 88f, 42f);
                DrawPanel(typingBubble, new Color(0.88f, 0.92f, 0.96f, 1f));
                GUI.Label(new Rect(typingBubble.x + 12f, typingBubble.y + 7f, typingBubble.width - 24f, 26f), "typing...", detailStyle);
            }

            GUI.EndScrollView();
        }

        string phoneHint = linkLoading
            ? "LOADING..."
            : imageOverlayVisible
            ? "UP ARROW  \u2022  CLOSE PHONE"
            : sequentialConversation && !CustomConversationComplete
                ? "MESSAGES..."
                : sceneConversation
            ? sceneReplySent ? "UP ARROW  \u2022  CLOSE PHONE" : "ENTER  \u2022  SEND NEVER"
            : "UP ARROW  \u2022  CLOSE PHONE";
        GUI.Label(
            new Rect(phone.x + 12f, phone.yMax - 32f, phone.width - 24f, 20f),
            phoneHint,
            hintStyle);

        GUI.color = oldColor;
        GUI.contentColor = oldContentColor;
    }

    private static void DrawPanel(Rect rect, Color color)
    {
        Color oldColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    private void ResetMessageScroll()
    {
        messageScroll = Vector2.zero;
        lastDrawnVisibleMessageCount = 0;
    }

    private void EnsureStyles()
    {
        if (headerStyle != null)
            return;

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };
        headerStyle.normal.textColor = Color.white;
        headerStyle.onNormal.textColor = Color.white;
        headerStyle.hover.textColor = Color.white;
        headerStyle.active.textColor = Color.white;
        headerStyle.focused.textColor = Color.white;

        detailStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 11
        };
        detailStyle.normal.textColor = new Color(0.82f, 0.88f, 0.94f);
        detailStyle.onNormal.textColor = detailStyle.normal.textColor;
        detailStyle.hover.textColor = detailStyle.normal.textColor;
        detailStyle.active.textColor = detailStyle.normal.textColor;
        detailStyle.focused.textColor = detailStyle.normal.textColor;

        incomingStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true,
            fontSize = 13
        };
        incomingStyle.normal.textColor = new Color(0.08f, 0.11f, 0.15f);

        outgoingStyle = new GUIStyle(incomingStyle);
        outgoingStyle.normal.textColor = new Color(0.98f, 0.99f, 1f);

        hintStyle = new GUIStyle(detailStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 10,
            fontStyle = FontStyle.Bold
        };
        hintStyle.normal.textColor = new Color(0.55f, 0.68f, 0.8f);
    }
}
