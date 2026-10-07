using ScrapFishing.Audio;
using ScrapFishing.Controls;
using ScrapFishing.Core;
using ScrapFishing.Dive;
using ScrapFishing.Scrap;
using ScrapFishing.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ScrapFishing.Boat
{
    public class BoatBootstrap : MonoBehaviour
    {
        GameFlow _flow;
        RunSession _session;
        DepthGauge _gauge;
        HookMover _hook;
        ScrapSpawner _spawner;
        CastingController _casting;
        DiveDirector _dive;
        VirtualJoystick _stick;
        TitleView _title;
        HudView _hud;
        ResultsView _results;
        SwipeTrail _trail;
        CastCamera _castCamera;
        BoatCanvas _canvasUi;

        void Awake()
        {
            AudioManager.Ensure();
            _flow = gameObject.AddComponent<GameFlow>();
            _session = gameObject.AddComponent<RunSession>();
            _canvasUi = FindFirstObjectByType<BoatCanvas>();
            if (_canvasUi == null)
            {
                Debug.LogError("Canvas에 BoatCanvas가 없습니다. 메뉴 Scrap Fishing > Build Scene UI를 실행하세요.");
            }

            ConfigureCamera();
            BuildWorld();
            BuildUi();
            BindInput();
            BindDive();
            _session.CaughtItem += AudioManager.Ensure().PlayCatch;
            _flow.PhaseChanged += HandlePhaseChanged;
        }

        void Start()
        {
            _title.SetVisible(true);
            _canvasUi.SetReadyVisible(true);
            _results.Hide();
            RefreshHud();
        }

        void Update()
        {
            TickSession();
            RefreshHud();
        }

        void ConfigureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            camera.orthographic = true;
            camera.orthographicSize = GameView.OrthoSize;
            camera.backgroundColor = Palette.Background;
            gameObject.AddComponent<FixedResolution>().Attach(camera);
        }

        void BuildWorld()
        {
            BuildSurface();
            var hookGo = CreateSprite("Hook", PlaceholderFactory.Circle(Palette.NeonGreen), new Vector3(SurfaceLayout.HookX, SurfaceLayout.WaterlineY, 0f), Vector3.one * 0.28f, 6);
            hookGo.transform.SetParent(transform, true);
            _hook = hookGo.AddComponent<HookMover>();
            _hook.Build(new Vector3(SurfaceLayout.HookX, SurfaceLayout.WaterlineY, 0f));

            _castCamera = gameObject.AddComponent<CastCamera>();
            _castCamera.Bind(Camera.main, _hook, _flow);

            _gauge = _canvasUi.DepthGauge;

            var spawnerGo = new GameObject("ScrapSpawner");
            spawnerGo.transform.SetParent(transform, false);
            _spawner = spawnerGo.AddComponent<ScrapSpawner>();

            _casting = gameObject.AddComponent<CastingController>();
            _casting.Bind(_flow, _session, _gauge, _hook, _spawner);

            var catcher = gameObject.AddComponent<SwipeCatcher>();
            catcher.Bind(_hook, _spawner, _session);

            var swipe = gameObject.AddComponent<SwipeReader>();
            swipe.Bind(Camera.main);
            _trail = gameObject.AddComponent<SwipeTrail>();
            _trail.Build();
            swipe.OnSwipe += info =>
            {
                if (_flow.Phase == GamePhase.Casting || _flow.Phase == GamePhase.Reeling)
                {
                    _trail.Show(info);
                    catcher.HandleSwipe(info);
                    return;
                }

                if (_flow.Phase == GamePhase.CastComplete && OfferDive() && info.IsDownward)
                {
                    StartDive();
                }
            };

            _dive = gameObject.AddComponent<DiveDirector>();
            _dive.Finished += HandleDiveFinished;
        }

        void BuildSurface()
        {
            var root = new GameObject("SurfaceProps");
            root.transform.SetParent(transform, false);

            var barge = CreateSprite("Barge", PlaceholderFactory.Square(new Color(0.18f, 0.2f, 0.26f)), SurfaceLayout.Barge, new Vector3(1.15f, 0.22f, 1f), 4);
            barge.transform.SetParent(root.transform, true);

            var fisher = CreateSprite("Fisher", PlaceholderFactory.Square(Palette.Cyan), SurfaceLayout.Fisher, new Vector3(0.22f, 0.38f, 1f), 5);
            fisher.transform.SetParent(root.transform, true);

            for (var i = 0; i < 4; i++)
            {
                var top = Mathf.Lerp(SurfaceLayout.WaterlineY, SurfaceLayout.MaxDepthY, i / 4f);
                var bottom = Mathf.Lerp(SurfaceLayout.WaterlineY, SurfaceLayout.MaxDepthY, (i + 1) / 4f);
                var height = top - bottom;
                var band = CreateSprite(
                    "ZoneWater" + i,
                    PlaceholderFactory.Square(DepthZone.WaterColor(i)),
                    new Vector3(0f, (top + bottom) * 0.5f, 0f),
                    new Vector3(5.6f, height + 0.08f, 1f),
                    -12 + i);
                band.transform.SetParent(root.transform, true);
            }

            var pad = CreateSprite(
                "ZoneWaterPad",
                PlaceholderFactory.Square(DepthZone.WaterColor(3)),
                new Vector3(0f, SurfaceLayout.MaxDepthY - 2f, 0f),
                new Vector3(5.6f, 4f, 1f),
                -9);
            pad.transform.SetParent(root.transform, true);
        }

        void BuildUi()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("Canvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(GameView.Width, GameView.Height);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = GameView.CanvasMatch;

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var eventGo = new GameObject("EventSystem");
                eventGo.AddComponent<EventSystem>();
                eventGo.AddComponent<InputSystemUIInputModule>();
            }

            _hud = canvas.gameObject.AddComponent<HudView>();
            _hud.Build(canvas.transform, _canvasUi.ChipText);
            _stick = canvas.gameObject.AddComponent<VirtualJoystick>();
            _stick.Build(canvas.transform);
            _title = canvas.gameObject.AddComponent<TitleView>();
            _title.Build(canvas.transform, BeginRun);
            _canvasUi.BringReadyToFront();
            _results = canvas.gameObject.AddComponent<ResultsView>();
            _results.Build(canvas.transform, _session.Restart);
        }

        void BindDive()
        {
            _dive.Bind(_spawner, _session, _gauge, _hook.gameObject, _stick);
        }

        void BindInput()
        {
            var tap = gameObject.AddComponent<TapReader>();
            tap.OnTap += HandleTap;
        }

        void BeginRun()
        {
            if (_flow.Phase != GamePhase.Title)
            {
                return;
            }

            _session.StartRun();
            _gauge.SetMaxDepth(Progression.MaxDepth);
            _title.SetVisible(false);
            _canvasUi.SetReadyVisible(false);
            _results.Hide();
            _flow.StartRun();
            _casting.ResetHook();
            AudioManager.Ensure().PlayBgm();
        }

        void HandleTap()
        {
            switch (_flow.Phase)
            {
                case GamePhase.Aiming:
                    _casting.CastFromGauge();
                    AudioManager.Ensure().PlayCast();
                    break;
                case GamePhase.CastComplete:
                    if (_session.IsExpired)
                    {
                        EndRun();
                        break;
                    }

                    Recast();
                    break;
            }
        }

        bool OfferDive()
        {
            return _session != null && _session.CanDive && !_session.IsExpired;
        }

        void StartDive()
        {
            _castCamera.SnapRest();
            _casting.ResetHook();
            AudioManager.Ensure().PlayDive();
            _flow.BeginDive();
            _dive.Begin();
        }

        void Recast()
        {
            _casting.ResetHook();
            _flow.ReturnToAiming();
        }

        void HandleDiveFinished()
        {
            if (_session.IsExpired)
            {
                EndRun();
                return;
            }

            _casting.ResetHook();
            _flow.ReturnToAiming();
        }

        void HandlePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.CastComplete && _session.IsExpired)
            {
                EndRun();
            }

            RefreshHud();
        }

        void TickSession()
        {
            if (_session == null || _flow == null)
            {
                return;
            }

            if (!_session.IsRunning)
            {
                return;
            }

            _session.Tick(Time.deltaTime);
            if (!_session.IsExpired)
            {
                return;
            }

            if (_flow.Phase == GamePhase.Aiming || _flow.Phase == GamePhase.CastComplete)
            {
                EndRun();
            }
        }

        void EndRun()
        {
            if (_flow.Phase == GamePhase.Results)
            {
                return;
            }

            _session.EndRun();
            if (_dive != null && _dive.IsActive)
            {
                _dive.Cancel();
            }

            _casting.ResetHook();
            _results.Show(_session);
            _title.SetVisible(false);
            _flow.ShowResults();
        }

        void RefreshHud()
        {
            if (_hud == null)
            {
                return;
            }

            _hud.Refresh(
                _flow,
                _session,
                _gauge != null ? _gauge.Normalized : 0f,
                _dive != null ? _dive.DiveFill : 0f,
                _dive != null && _dive.IsForcedAscent);
            if (_gauge != null)
            {
                _gauge.gameObject.SetActive(_flow.Phase != GamePhase.Title && _flow.Phase != GamePhase.Results && _flow.Phase != GamePhase.Diving);
            }
        }

        static GameObject CreateSprite(string name, Sprite sprite, Vector3 position, Vector3 scale, int order)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.transform.localScale = scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return go;
        }
    }
}
