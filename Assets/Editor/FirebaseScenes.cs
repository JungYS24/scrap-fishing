using System.Linq;
using ScrapFishing.Net;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ScrapFishing.Build
{
    // 네트워크 프로그래밍 10강 실습 5·7의 씬 구성을 메뉴 한 번으로 만든다.
    // - Login 씬: AuthManager(Api Key, Status Text) + "게스트 로그인" 버튼(On Click → GuestLogin)
    // - Menu 씬: UidText + MenuUI + "출항" 버튼(→ Boat) + "로그아웃" 버튼(On Click → Logout)
    // - Boat 씬: ScoreSaver(Db Url)
    // - Scene List: Login(0) → Menu → Boat
    // Firebase 웹 API 키는 공개용 식별자라 빌드에 들어가도 된다(보안은 Realtime Database 규칙이 맡는다).
    public static class FirebaseScenes
    {
        public const string LoginScene = "Assets/Scenes/Login.unity";
        public const string MenuScene = "Assets/Scenes/Menu.unity";
        public const string ApiKey = "AIzaSyDr6P8HXSo9yf-XSx80fitJop-mHVYuq7Y";
        // 실습 6에서 Singapore(asia-southeast1)로 만든 기본 인스턴스 주소. 다른 리전으로 만들었으면 콘솔에 보이는 주소로 바꾼다(끝에 / 없이).
        public const string DbUrl = "https://networkpro2026-b25f3-default-rtdb.asia-southeast1.firebasedatabase.app";

        public static readonly string[] BuildScenes = { LoginScene, MenuScene, WebGLBuilder.BoatScene };

        static readonly Color Background = new Color(0.03f, 0.05f, 0.11f);
        static readonly Color Green = new Color(0.22f, 1f, 0.55f);
        static readonly Color Magenta = new Color(1f, 0.25f, 0.75f);
        static readonly Color Cyan = new Color(0.35f, 0.9f, 1f);

        [MenuItem("Tools/Firebase/Create Login & Menu Scenes")]
        public static void CreateAll()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            CreateLogin();
            CreateMenu();
            AddScoreSaverToBoat();
            SetBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("Firebase scenes ready: " + string.Join(" → ", BuildScenes));
        }

        static void CreateLogin()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var canvas = CreateBase();

            CreateText(canvas, "Title", "SCRAP FISHING", 42, Green, new Vector2(0.05f, 0.68f), new Vector2(0.95f, 0.8f));
            CreateText(canvas, "Subtitle", "Firebase 게스트 로그인", 22, Cyan, new Vector2(0.05f, 0.6f), new Vector2(0.95f, 0.66f));
            var status = CreateText(canvas, "StatusText", "", 22, Color.white, new Vector2(0.05f, 0.3f), new Vector2(0.95f, 0.38f));

            var auth = new GameObject("AuthManager").AddComponent<AuthManager>();
            auth.apiKey = ApiKey;
            auth.statusText = status;

            var login = CreateButton(canvas, "GuestLoginButton", "게스트 로그인", Green, new Vector2(0.15f, 0.42f), new Vector2(0.85f, 0.52f));
            UnityEventTools.AddPersistentListener(login.onClick, auth.GuestLogin);

            EditorSceneManager.SaveScene(scene, LoginScene);
        }

        static void CreateMenu()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var canvas = CreateBase();

            CreateText(canvas, "Title", "MENU", 42, Green, new Vector2(0.05f, 0.7f), new Vector2(0.95f, 0.8f));
            var uid = CreateText(canvas, "UidText", "게스트", 28, Color.white, new Vector2(0.05f, 0.6f), new Vector2(0.95f, 0.67f));

            var menu = new GameObject("MenuUI").AddComponent<MenuUI>();
            menu.uidText = uid;

            var start = CreateButton(canvas, "StartButton", "출항", Green, new Vector2(0.15f, 0.44f), new Vector2(0.85f, 0.54f));
            UnityEventTools.AddPersistentListener(start.onClick, menu.StartGame);
            var logout = CreateButton(canvas, "LogoutButton", "로그아웃", Magenta, new Vector2(0.15f, 0.3f), new Vector2(0.85f, 0.4f));
            UnityEventTools.AddPersistentListener(logout.onClick, menu.Logout);

            EditorSceneManager.SaveScene(scene, MenuScene);
        }

        static void AddScoreSaverToBoat()
        {
            var scene = EditorSceneManager.OpenScene(WebGLBuilder.BoatScene, OpenSceneMode.Single);
            var saver = Object.FindFirstObjectByType<ScoreSaver>();
            if (saver == null)
            {
                saver = new GameObject("ScoreSaver").AddComponent<ScoreSaver>();
            }

            saver.dbUrl = DbUrl;
            EditorUtility.SetDirty(saver);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void SetBuildScenes()
        {
            var list = BuildScenes.Select(path => new EditorBuildSettingsScene(path, true)).ToList();
            foreach (var old in EditorBuildSettings.scenes)
            {
                if (!BuildScenes.Contains(old.path))
                {
                    list.Add(new EditorBuildSettingsScene(old.path, false));
                }
            }

            EditorBuildSettings.scenes = list.ToArray();
        }

        // 카메라·EventSystem·Canvas. 게임과 같은 세로 480×854 기준으로 맞춘다.
        static Transform CreateBase()
        {
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.orthographic = true;

            var eventGo = new GameObject("EventSystem");
            eventGo.AddComponent<EventSystem>();
            eventGo.AddComponent<InputSystemUIInputModule>();

            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(480, 854);
            scaler.matchWidthOrHeight = 1f;
            canvasGo.AddComponent<GraphicRaycaster>();
            return canvasGo.transform;
        }

        static Text CreateText(Transform parent, string name, string value, int size, Color color, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, min, max);
            var text = go.AddComponent<Text>();
            text.font = Resources.Load<Font>(size >= 28 ? "Fonts/Galmuri14" : "Fonts/Galmuri11");
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = color;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        static Button CreateButton(Transform parent, string name, string label, Color color, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, min, max);
            var image = go.AddComponent<Image>();
            image.color = new Color(color.r * 0.15f, color.g * 0.15f, color.b * 0.15f, 1f);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(3f, -3f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            CreateText(go.transform, "Label", label, 28, color, Vector2.zero, Vector2.one);
            return button;
        }

        static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
