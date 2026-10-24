using ScrapFishing.Audio;
using ScrapFishing.Controls;
using ScrapFishing.Core;
using ScrapFishing.Dive;
using ScrapFishing.Fishing;
using ScrapFishing.FX;
using ScrapFishing.Gacha;
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
        LureCollector _collector;
        DiveDirector _dive;
        VirtualJoystick _stick;
        TitleView _title;
        HudView _hud;
        ResultsView _results;
        SwipeTrail _trail;
        CastCamera _castCamera;
        BoatCanvas _canvasUi;
        GachaShopView _market;
        SettingsView _settingsView;
        AnglerRig _rig;

        void Awake()
        {
            Progression.MigrateSave();
            Fx.Ensure();
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
            _session.DiveCharged += AudioManager.Ensure().PlayDiveReady;
            _flow.PhaseChanged += HandlePhaseChanged;
        }

        void Start()
        {
            Fx.FadeIn(0.22f);
            AudioManager.Ensure().PlayTitleBgm();
            _title.SetVisible(true);
            _canvasUi.SetReadyVisible(true);
            _results.Hide();
            RefreshHud();
            GearBonuses.Changed += ComposeAngler;
            ComposeAngler();
        }

        void OnDestroy()
        {
            GearBonuses.Changed -= ComposeAngler;
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
            _hook = FindFirstObjectByType<HookMover>(FindObjectsInactive.Include);
            if (_hook == null)
            {
                Debug.LogWarning("씬에 Lure(HookMover)가 없어 임시 루어를 만듭니다. 메뉴 Scrap Fishing > Build Boat Scene을 실행하세요.");
                var hookGo = CreateSprite("Lure", PlaceholderFactory.Circle(Palette.NeonGreen), Vector3.zero, Vector3.one * 0.28f, 6);
                _hook = hookGo.AddComponent<HookMover>();
            }

            _hook.gameObject.SetActive(true);
            _hook.Build(new Vector3(SurfaceLayout.HookX, SurfaceLayout.WaterlineY, 0f));

            _gauge = _canvasUi.DepthGauge;

            var spawnerGo = new GameObject("ScrapSpawner");
            spawnerGo.transform.SetParent(transform, false);
            _spawner = spawnerGo.AddComponent<ScrapSpawner>();

            _casting = GetComponent<CastingController>();
            if (_casting == null)
            {
                _casting = gameObject.AddComponent<CastingController>();
            }

            _collector = _hook.GetComponent<LureCollector>();
            if (_collector == null)
            {
                _collector = _hook.gameObject.AddComponent<LureCollector>();
            }

            _collector.SetSettlePoint(EnsureSettlePoint());
            _casting.Bind(_flow, _session, _gauge, _hook, _spawner, _collector);

            _castCamera = gameObject.AddComponent<CastCamera>();
            _castCamera.Bind(Camera.main, _hook, _flow, _casting);
            gameObject.AddComponent<DepthBubbles>().Bind(Camera.main, _casting);

            var catcher = GetComponent<SwipeCatcher>();
            if (catcher == null)
            {
                catcher = gameObject.AddComponent<SwipeCatcher>();
            }

            catcher.Bind(_hook, _spawner, _session, _collector);

            var swipe = gameObject.AddComponent<SwipeReader>();
            swipe.Bind(Camera.main);
            _trail = gameObject.AddComponent<SwipeTrail>();
            _trail.Build();
            var steer = gameObject.AddComponent<SteerReader>();
            steer.Bind(Camera.main, () => _casting.AcceptsSteer);
            steer.OnWorldDeltaX += _casting.AddSteerDelta;
            steer.OnSegment += _trail.Extend;
            var strokeHit = false;
            swipe.OnBegin += () =>
            {
                strokeHit = false;
                _trail.Begin();
            };
            swipe.OnDrag += info =>
            {
                if ((_flow.Phase != GamePhase.Casting && _flow.Phase != GamePhase.Reeling) || _casting.IsLaunching)
                {
                    return;
                }

                if (!strokeHit && catcher.HandleSwipe(info))
                {
                    strokeHit = true;
                }
            };
            swipe.OnSwipe += info =>
            {
                if (info.IsDownward)
                {
                    HandleDiveSwipe();
                }
            };

            _dive = gameObject.AddComponent<DiveDirector>();
            _dive.Finished += HandleDiveFinished;
        }

        // 보트 위 주인공을 장착한 보트·피셔·낚싯대(없으면 normal_01 외형)로 다시 조합한다. 장착은 타이틀·결과 화면에서만 바뀐다.
        void ComposeAngler()
        {
            if (_rig == null)
            {
                _rig = AnglerRig.Attach(GameObject.Find("Nomad"));
                if (_rig == null)
                {
                    return;
                }
            }

            _rig.Compose(_market != null && _market.Shop != null ? _market.Shop.Catalog : null);
        }

        void BuildSurface()
        {
            var root = GameObject.Find("SurfaceProps");
            if (root == null)
            {
                Debug.LogWarning("씬에 SurfaceProps(Nomad)가 없습니다. 메뉴 Scrap Fishing > Build Boat Scene을 실행하세요.");
                root = new GameObject("SurfaceProps");
            }

            for (var i = 0; i < 4; i++)
            {
                var name = "ZoneWater" + i;
                var existing = root.transform.Find(name);
                GameObject band;
                if (existing != null)
                {
                    band = existing.gameObject;
                }
                else
                {
                    var top = Mathf.Lerp(SurfaceLayout.WaterlineY, SurfaceLayout.MaxDepthY, i / 4f);
                    var bottom = Mathf.Lerp(SurfaceLayout.WaterlineY, SurfaceLayout.MaxDepthY, (i + 1) / 4f);
                    var height = top - bottom;
                    band = CreateSprite(
                        name,
                        PlaceholderFactory.Square(DepthZone.WaterColor(i)),
                        new Vector3(0f, (top + bottom) * 0.5f, 0f),
                        new Vector3(5.6f, height + 0.08f, 1f),
                        -12 + i);
                    band.transform.SetParent(root.transform, true);
                }

                HideColorBand(band);
            }

            var padTransform = root.transform.Find("ZoneWaterPad");
            GameObject pad;
            if (padTransform != null)
            {
                pad = padTransform.gameObject;
            }
            else
            {
                pad = CreateSprite(
                    "ZoneWaterPad",
                    PlaceholderFactory.Square(DepthZone.WaterColor(3)),
                    new Vector3(0f, SurfaceLayout.MaxDepthY - 2f, 0f),
                    new Vector3(5.6f, 4f, 1f),
                    -9);
                pad.transform.SetParent(root.transform, true);
            }

            HideColorBand(pad);
            if (root.TryGetComponent<ZoneBackdrop>(out var leftover))
            {
                Destroy(leftover);
            }

            var host = GameObject.Find("ZoneBackdropRoot");
            if (host == null)
            {
                host = new GameObject("ZoneBackdropRoot");
            }

            host.transform.SetParent(null);
            host.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            host.transform.localScale = Vector3.one;
            if (!host.TryGetComponent<ZoneBackdrop>(out _))
            {
                host.AddComponent<ZoneBackdrop>();
            }
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

            var area = GameAreaFit.Ensure(canvas);
            _hud = canvas.gameObject.AddComponent<HudView>();
            _hud.Build(area.Area, _canvasUi.ChipText, _canvasUi.ChipPanel);
            _hud.Bind(_session);
            if (_casting != null)
            {
                _casting.SetHud(_hud);
            }

            canvas.gameObject.AddComponent<FirstPlayGuide>().Bind(area.Area, _flow, _casting);
            if (_collector != null)
            {
                _collector.Bind(_session, EnsureSettlePoint(), _hud);
            }
            _stick = canvas.gameObject.AddComponent<VirtualJoystick>();
            _stick.Build(area.Area);
            _title = _canvasUi.Title;
            _title.Bind(BeginRun);
            _title.BindMarket(OpenMarket);
            _title.BindSettings(OpenSettings);
            _canvasUi.BringReadyToFront();
            _results = canvas.gameObject.AddComponent<ResultsView>();
            _results.Build(area.Area, _session.Restart, OpenMarket, OpenSettings);
            _market = canvas.gameObject.AddComponent<GachaShopView>();
            _market.Build(area.Area);
            _settingsView = canvas.gameObject.AddComponent<SettingsView>();
            _settingsView.Build(area.Area);
            area.Adopt();
        }

        void OpenMarket()
        {
            if (_market == null || (_flow.Phase != GamePhase.Title && _flow.Phase != GamePhase.Results))
            {
                return;
            }

            _market.Open();
        }

        // 설정창은 타이틀·결과 화면에서만 연다. 플레이 중에는 소리 토글만 쓴다.
        void OpenSettings()
        {
            if (_settingsView == null || (_market != null && _market.IsOpen)
                || (_flow.Phase != GamePhase.Title && _flow.Phase != GamePhase.Results))
            {
                return;
            }

            _settingsView.Open();
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

            Fx.PulseFade(0.22f);
            AudioManager.Ensure().PlayStart();
            _session.StartRun();
            _spawner.ResetRun();
            _hud.NotifyRunReset();
            _gauge.SetMaxDepth(GearBonuses.MaxDepth(Progression.MaxDepth));
            _title.SetVisible(false);
            _canvasUi.SetReadyVisible(false);
            _results.Hide();
            _flow.StartRun();
            _casting.PrepareForRun();
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

        void HandleDiveSwipe()
        {
            if (_flow.Phase != GamePhase.Aiming && _flow.Phase != GamePhase.CastComplete)
            {
                return;
            }

            if (OfferDive())
            {
                StartDive();
            }
            else if (!_session.HasDiveCharge)
            {
                _hud.Notify($"잠수까지 캐스팅 {_session.CastsUntilDive}회");
            }
            else if (!_session.HasDiveTime)
            {
                _hud.Notify("남은 시간이 부족해 잠수 불가");
            }
        }

        void StartDive()
        {
            Fx.PulseFade(0.25f);
            _castCamera.SnapRest();
            _casting.ResetHook();
            AudioManager.Ensure().PlayDive();
            AudioManager.Ensure().SetDiveMix(true);
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

            Fx.PulseFade(0.25f);
            AudioManager.Ensure().SetDiveMix(false);
            _casting.ResetHook();
            _flow.ReturnToAiming();
        }

        void HandlePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.CastComplete && !_session.IsExpired)
            {
                _session.RegisterCastComplete();
            }

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

            if (_collector != null)
            {
                _collector.SettleImmediate();
            }

            AudioManager.Ensure().PlayTimeUp();
            AudioManager.Ensure().DuckResults();
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

            _hud.SetSteerGuide(_casting != null && _casting.ShowSteerGuide);
            if (_collector != null && _flow.Phase != GamePhase.Diving && _flow.Phase != GamePhase.Title && _flow.Phase != GamePhase.Results)
            {
                _hud.SetLoad(_collector.Count, _collector.MaxAttach);
            }
        }

        Transform EnsureSettlePoint()
        {
            var existing = GameObject.Find("SettlePoint");
            if (existing != null)
            {
                return existing.transform;
            }

            var parent = GameObject.Find("SurfaceProps");
            var point = new GameObject("SettlePoint");
            if (parent != null)
            {
                point.transform.SetParent(parent.transform, true);
            }

            point.transform.position = new Vector3(0f, 0.85f, 0f);
            return point.transform;
        }

        static void SetSpriteAlpha(GameObject go, float alpha)
        {
            if (go == null || !go.TryGetComponent<SpriteRenderer>(out var renderer))
            {
                return;
            }

            var color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }

        static void HideColorBand(GameObject go)
        {
            SetSpriteAlpha(go, 0f);
            if (go != null && go.TryGetComponent<SpriteRenderer>(out var renderer))
            {
                renderer.enabled = false;
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
