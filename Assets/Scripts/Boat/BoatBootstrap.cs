using ScrapFishing.Audio;
using ScrapFishing.Controls;
using ScrapFishing.Core;
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
        CastingController _casting;
        TitleView _title;
        HudView _hud;
        ResultsView _results;

        void Awake()
        {
            AudioManager.Ensure();
            _flow = gameObject.AddComponent<GameFlow>();
            _session = gameObject.AddComponent<RunSession>();

            ConfigureCamera();
            BuildWorld();
            BuildUi();
            BindInput();
            _flow.PhaseChanged += HandlePhaseChanged;
        }

        void Start()
        {
            _title.SetVisible(true);
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
            var hookGo = CreateSprite("Hook", PlaceholderFactory.Circle(Palette.NeonGreen), new Vector3(0f, 3.55f, 0f), Vector3.one * 0.34f, 6);
            hookGo.transform.SetParent(transform, true);
            _hook = hookGo.AddComponent<HookMover>();
            _hook.Build(new Vector3(0f, 3.55f, 0f));

            var gaugeGo = new GameObject("DepthGauge");
            _gauge = gaugeGo.AddComponent<DepthGauge>();
            _gauge.Build(transform);

            var spawnerGo = new GameObject("ScrapSpawner");
            spawnerGo.transform.SetParent(transform, false);
            var spawner = spawnerGo.AddComponent<ScrapSpawner>();

            _casting = gameObject.AddComponent<CastingController>();
            _casting.Bind(_flow, _session, _gauge, _hook, spawner);

            var catcher = gameObject.AddComponent<SwipeCatcher>();
            catcher.Bind(_hook, spawner, _session);

            var swipe = gameObject.AddComponent<SwipeReader>();
            swipe.Bind(Camera.main);
            swipe.OnSwipe += info =>
            {
                if (_flow.Phase == GamePhase.Reeling)
                {
                    catcher.HandleSwipe(info);
                }
            };
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

            _title = canvas.gameObject.AddComponent<TitleView>();
            _title.Build(canvas.transform);
            _hud = canvas.gameObject.AddComponent<HudView>();
            _hud.Build(canvas.transform);
            _results = canvas.gameObject.AddComponent<ResultsView>();
            _results.Build(canvas.transform);
        }

        void BindInput()
        {
            var tap = gameObject.AddComponent<TapReader>();
            tap.OnTap += HandleTap;
        }

        void HandleTap()
        {
            switch (_flow.Phase)
            {
                case GamePhase.Title:
                    _session.ResetRun();
                    _title.SetVisible(false);
                    _results.Hide();
                    _flow.StartRun();
                    _casting.ResetHook();
                    AudioManager.Ensure().PlayBgm();
                    break;
                case GamePhase.Aiming:
                    _casting.CastFromGauge();
                    break;
                case GamePhase.CastComplete:
                    if (_session.IsExpired)
                    {
                        EndRun();
                        break;
                    }

                    _casting.ResetHook();
                    _flow.ReturnToAiming();
                    break;
                case GamePhase.Results:
                    _results.Hide();
                    _title.SetVisible(true);
                    _casting.ResetHook();
                    _flow.ReturnToTitle();
                    break;
            }
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

            _session.Stop();
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

            _hud.Refresh(_flow, _session, _gauge != null ? _gauge.Normalized : 0f);
            if (_gauge != null)
            {
                _gauge.gameObject.SetActive(_flow.Phase != GamePhase.Title && _flow.Phase != GamePhase.Results);
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
