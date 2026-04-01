using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PremiumLudo
{
    [DefaultExecutionOrder(-500)]
    public sealed class LudoBootstrap : MonoBehaviour
    {
        private static bool s_RuntimeCreated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureBootstrap()
        {
            if (FindAnyObjectByType<LudoBootstrap>() != null)
            {
                return;
            }

            GameObject bootstrap = new GameObject("LudoBootstrap");
            bootstrap.AddComponent<LudoBootstrap>();
            s_RuntimeCreated = true;
        }

        private void Awake()
        {
            if (!s_RuntimeCreated && FindObjectsByType<LudoBootstrap>().Length > 1)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Input.multiTouchEnabled = true;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            AudioListener.pause = false;
            AudioListener.volume = 1f;

            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                QualitySettings.SetQualityLevel(1, true);
            }

            EnsureCamera();
            Canvas canvas = EnsureCanvas();
            EnsureEventSystem();

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            LudoSafeAreaController safeAreaController = LudoUtility.GetOrAddComponent<LudoSafeAreaController>(canvas.gameObject);

            RectTransform boardLayer = EnsureLayer(canvasRect, "BoardLayer");
            RectTransform tokenLayer = EnsureLayer(canvasRect, "TokenLayer");
            RectTransform effectsLayer = EnsureLayer(canvasRect, "EffectsLayer");
            RectTransform uiLayer = EnsureLayer(canvasRect, "UILayer");

            boardLayer.SetSiblingIndex(0);
            tokenLayer.SetSiblingIndex(1);
            effectsLayer.SetSiblingIndex(2);
            uiLayer.SetSiblingIndex(3);

            safeAreaController.Register(boardLayer);
            safeAreaController.Register(tokenLayer);
            safeAreaController.Register(effectsLayer);
            safeAreaController.Register(uiLayer);

            LudoAnimationController animationController = LudoUtility.GetOrAddComponent<LudoAnimationController>(gameObject);

            LudoBoardRenderer boardRenderer = LudoUtility.GetOrAddComponent<LudoBoardRenderer>(boardLayer.gameObject);
            boardRenderer.Initialize(boardLayer);

            LudoGameController gameController = LudoUtility.GetOrAddComponent<LudoGameController>(gameObject);
            gameController.Initialize(animationController, boardRenderer, tokenLayer, effectsLayer, uiLayer);

            LudoAppController appController = LudoUtility.GetOrAddComponent<LudoAppController>(gameObject);
            appController.Initialize(animationController, boardRenderer, gameController, uiLayer);
        }

        private static Camera EnsureCamera()
        {
            Camera camera = FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.20f, 0.54f, 1f);
            camera.orthographic = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.cullingMask = 0;
            camera.depth = -10f;

            if (camera.GetComponent<AudioListener>() == null)
            {
                camera.gameObject.AddComponent<AudioListener>();
            }

            return camera;
        }

        private static Canvas EnsureCanvas()
        {
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("Canvas");
                canvas = canvasObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            CanvasScaler scaler = LudoUtility.GetOrAddComponent<CanvasScaler>(canvas.gameObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            LudoUtility.GetOrAddComponent<GraphicRaycaster>(canvas.gameObject);
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
                eventSystemObject.AddComponent<StandaloneInputModule>();
            }
            else if (eventSystem.GetComponent<BaseInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }
        }

        private static RectTransform EnsureLayer(RectTransform parent, string name)
        {
            Transform existing = parent.Find(name);
            RectTransform layer = existing != null ? existing as RectTransform : LudoUtility.CreateUIObject(name, parent);
            if (layer == null)
            {
                layer = LudoUtility.CreateUIObject(name, parent);
            }

            LudoUtility.Stretch(layer);
            return layer;
        }
    }
}
