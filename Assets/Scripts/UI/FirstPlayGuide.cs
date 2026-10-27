using System;
using System.Collections.Generic;
using DG.Tweening;
using ScrapFishing.Boat;
using ScrapFishing.Controls;
using ScrapFishing.Core;
using ScrapFishing.Scrap;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    // 첫 출항 튜토리얼. 딤 4장(위·아래·왼쪽·오른쪽 Image)으로 강조 구멍을 뚫고, 구멍 네 모서리 꺾쇠·은은한 빛·손가락 동작·안내 패널을 띄운다.
    // 스텐실·마스크 셰이더를 쓰지 않아 WebGL에서도 똑같이 보인다. 스킨(Resources/UI/TutorialSkin)이 없으면 단색 패널·노란 사각형으로 그린다.
    // 별도 Canvas(overrideSorting)라 달러 패널(UiStayOnTop)을 포함한 HUD 전체보다 위에 그려지고, 설정창이 열리면 잠깐 숨는다.
    //  1) 조준: 바늘을 보고 톡 터치. 바늘은 계속 움직이고 제한 시간만 멈춘다.
    //  2) 첫 캐스팅 중: 좌우로 쓱 문질러 갈고리 조종. 게임 전체 정지.
    //  3) 첫 인양 뒤: HUD 시간·스크랩 안내. 게임 전체 정지. 닫으면 TutorialDone=1.
    //  4) 잠수 칸이 다 찼을 때: 아래로 스와이프해 잠수. 제한 시간만 멈추고 입력은 막지 않는다. 잠수하면 TutorialDone=2.
    //  5) 그 잠수에서: 조이스틱 안내. 게임 전체 정지.
    public class FirstPlayGuide : MonoBehaviour
    {
        public const string Key = "TutorialDone";
        public const int StepCount = 5;
        const int BasicDone = 1;
        const int Done = 2;

        // 단계가 뜬 직후 연타로 바로 넘어가지 않게 입력을 무시하는 시간(실시간).
        const float InputGuard = 0.45f;
        const float FadeTime = 0.22f;
        const float SteerDelay = 0.6f;
        const float SwimDelay = 0.45f;
        const float ScrapNearRadius = 1.6f;
        const float DragStartPixels = 14f;
        const float SteerBandHalf = 90f;
        const float TapPress = 0.16f;
        const float TapDown = 0.46f;
        const float TapRing = 0.42f;
        const float TapCycle = 0.9f;
        const float RingGap = 0.4f;
        const int SortingAbove = 50;

        // 480×854 기준 크기. 패널 폭은 화면 폭의 92%.
        const float PanelWidthRatio = 0.92f;
        const float PanelTop = 38f;
        const float TitleHeight = 36f;
        const float BodyLine = 38f;
        const float TextInset = 30f;
        const float ButtonWidth = 220f;
        const float ButtonHeight = 56f;
        const float TabHeight = 36f;
        const float TabWidth = 196f;
        const float DotSize = 11f;
        const float DotGap = 6f;
        const float ScreenMargin = 12f;
        const float BracketWidth = 20f;
        const float BracketSpread = 4f;
        const float HandWidth = 64f;
        const float RingSize = 64f;
        const float ArrowNudge = 6f;
        // 스킨 9-slice 테두리를 반으로 줄여(원본 PNG는 2배 크기) 480 폭 화면에 맞춘다.
        const float SkinBorderScale = 2f;

        const string Yellow = "#FFE14D";
        const string Mint = "#5CFFD1";
        static readonly Color DimColor = new Color(0f, 0f, 0f, 0.45f);
        static readonly Color FrameColor = new Color(1f, 0.88f, 0.3f, 1f);
        static readonly Color CardColor = new Color(0.05f, 0.07f, 0.13f, 0.97f);

        enum Step
        {
            None,
            Aim,
            Steer,
            Timer,
            DiveSwipe,
            Swim
        }

        enum FingerMotion
        {
            None,
            Tap,
            Swipe,
            Circle,
            SwipeDown
        }

        GameFlow _flow;
        CastingController _casting;
        RunSession _session;
        ScrapSpawner _spawner;
        HookMover _hook;
        DepthGauge _gauge;
        VirtualJoystick _stick;
        HudView _hud;
        RectTransform _chipPanel;
        Action _tapThrough;
        Func<bool> _overlayOpen;

        TutorialSkin _skin;
        RectTransform _root;
        Canvas _canvas;
        int _sortingOrder;
        CanvasGroup _group;
        Camera _uiCamera;
        readonly Image[] _dims = new Image[4];
        Image _glow;
        RectTransform _frame;
        readonly RectTransform[] _brackets = new RectTransform[4];
        RectTransform _arrow;
        RectTransform _arrowDown;
        readonly RectTransform[] _rings = new RectTransform[2];
        readonly Image[] _ringImages = new Image[2];
        RectTransform _finger;
        GameObject _blocker;
        RectTransform _card;
        Text _badge;
        Text _title;
        Text _body;
        Button _button;
        Text _buttonLabel;
        readonly List<Image> _dots = new List<Image>();

        Step _next;
        Step _showing;
        Rect _hole;
        Rect _cardBounds;
        float _cardHeight;
        bool _cardHasButton;
        float _guardUntil;
        float _steerTime;
        float _swimTime;
        bool _pausedGame;
        float _scaleBefore = 1f;
        bool _pressInside;
        Vector2 _pressAt;
        float _spread;
        Vector2 _arrowBase;
        Vector2 _arrowDir;
        Vector2 _tapOrigin;
        Sequence _motion;
        readonly List<Tween> _loops = new List<Tween>();
        Tween _fade;

        public void Bind(RectTransform parent, GameFlow flow, CastingController casting)
        {
            Bind(parent, flow, casting, null, null, null, null, null, null, null, null);
        }

        // tapThrough: 1단계에서 딤·패널을 톡 눌러도 화면 터치처럼 낚싯줄을 던지게 한다.
        public void Bind(
            RectTransform parent,
            GameFlow flow,
            CastingController casting,
            RunSession session,
            ScrapSpawner spawner,
            HookMover hook,
            DepthGauge gauge,
            VirtualJoystick stick,
            HudView hud,
            GameObject chipPanel,
            Action tapThrough)
        {
            _flow = flow;
            _casting = casting;
            _session = session;
            _spawner = spawner;
            _hook = hook;
            _gauge = gauge;
            _stick = stick;
            _hud = hud;
            _chipPanel = chipPanel != null ? chipPanel.transform as RectTransform : null;
            _tapThrough = tapThrough;

            var saved = PlayerPrefs.GetInt(Key, 0);
            if (saved >= Done || parent == null || _flow == null)
            {
                return;
            }

            _next = saved >= BasicDone ? Step.DiveSwipe : Step.Aim;
            Build(parent);
            _flow.PhaseChanged += HandlePhaseChanged;
            Loc.Changed += Relabel;
        }

        // 설정창에서 언어를 바꾸면 떠 있는 안내 카드의 글자와 높이를 다시 맞춘다.
        void Relabel()
        {
            if (_showing == Step.None || _card == null)
            {
                return;
            }

            Fill(_showing);
            PlaceCard(_hole, _showing == Step.Aim || _showing == Step.DiveSwipe);
        }

        // 설정창처럼 튜토리얼보다 위에 와야 하는 창이 열려 있는지. 열려 있는 동안 튜토리얼을 숨긴다.
        public void BindOverlay(Func<bool> overlayOpen)
        {
            _overlayOpen = overlayOpen;
        }

        void OnDestroy()
        {
            if (_flow != null)
            {
                _flow.PhaseChanged -= HandlePhaseChanged;
            }

            Loc.Changed -= Relabel;
            KillLoops();
            _fade?.Kill();
        }

        void HandlePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Casting)
            {
                _steerTime = 0f;
            }

            if (phase == GamePhase.Diving)
            {
                _swimTime = 0f;
            }

            // 결과·타이틀로 넘어가면 그대로 닫는다. 시간 배율은 EndRun·Restart가 정하므로 되돌리지 않는다.
            if ((phase == GamePhase.Results || phase == GamePhase.Title) && _showing != Step.None)
            {
                Hide(false, false);
            }
        }

        void Update()
        {
            if (_root == null)
            {
                return;
            }

            var covered = _overlayOpen != null && _overlayOpen();
            if (_canvas.enabled == covered)
            {
                _canvas.enabled = !covered;
            }

            if (_showing != Step.None)
            {
                if (!covered)
                {
                    TickShowing();
                }

                return;
            }

            // 설정창 등 다른 이유로 멈춰 있으면 새 단계를 띄우지 않는다.
            if (covered || Time.timeScale <= 0f)
            {
                return;
            }

            switch (_next)
            {
                case Step.Aim:
                    if (_flow.Phase == GamePhase.Aiming && _gauge != null && _gauge.gameObject.activeInHierarchy)
                    {
                        Show(Step.Aim);
                    }

                    break;
                case Step.Steer:
                    if (_casting != null && _casting.AcceptsSteer)
                    {
                        _steerTime += Time.deltaTime;
                        var near = _spawner != null && _hook != null && _spawner.HasScrapNear(_hook.Position, ScrapNearRadius);
                        if (near || _steerTime >= SteerDelay)
                        {
                            Show(Step.Steer);
                        }
                    }

                    break;
                case Step.Timer:
                    if (_flow.Phase == GamePhase.CastComplete && (_session == null || !_session.IsExpired))
                    {
                        Show(Step.Timer);
                    }

                    break;
                case Step.DiveSwipe:
                    if (DiveSwipeReady())
                    {
                        Show(Step.DiveSwipe);
                    }

                    break;
                case Step.Swim:
                    if (_flow.Phase == GamePhase.Diving && _stick != null && _stick.IsVisible)
                    {
                        _swimTime += Time.deltaTime;
                        if (_swimTime >= SwimDelay)
                        {
                            Show(Step.Swim);
                        }
                    }

                    break;
            }
        }

        // 잠수 칸이 다 차서 지금 아래로 스와이프하면 잠수할 수 있는 순간(BoatBootstrap.HandleDiveSwipe와 같은 조건).
        bool DiveSwipeReady()
        {
            return _session != null
                && _session.CanDive
                && !_session.IsExpired
                && (_flow.Phase == GamePhase.Aiming || _flow.Phase == GamePhase.CastComplete);
        }

        void TickShowing()
        {
            if (TryHole(_showing, out var hole))
            {
                ApplyHole(hole);
            }

            var guarded = Time.unscaledTime < _guardUntil;
            SetActive(_blocker, guarded || _showing == Step.Timer);
            if (_button != null)
            {
                _button.interactable = !guarded;
            }

            switch (_showing)
            {
                case Step.Aim:
                    if (_flow.Phase != GamePhase.Aiming)
                    {
                        Complete();
                    }

                    break;
                case Step.Steer:
                    if (_flow.Phase != GamePhase.Casting && _flow.Phase != GamePhase.Reeling)
                    {
                        Complete();
                    }
                    else if (!guarded && DraggedInHole())
                    {
                        Complete();
                    }

                    break;
                case Step.Timer:
                    if (_flow.Phase != GamePhase.CastComplete)
                    {
                        Complete();
                    }

                    break;
                case Step.DiveSwipe:
                    // 잠수에 들어가야 완료. 다시 던지는 등 다른 길로 가면 닫고, 다음에 잠수할 수 있을 때 다시 띄운다.
                    if (_flow.Phase == GamePhase.Diving)
                    {
                        Complete();
                    }
                    else if (!DiveSwipeReady())
                    {
                        Hide(true, true);
                    }

                    break;
                case Step.Swim:
                    if (_flow.Phase != GamePhase.Diving)
                    {
                        Complete();
                    }
                    else if (!guarded && JoystickMoved())
                    {
                        Complete();
                    }

                    break;
            }
        }

        void Show(Step step)
        {
            if (!TryHole(step, out var hole))
            {
                // 대상이 잠깐 꺼져 있으면 다음 프레임에 다시 본다. 대상 자체가 없을 때만 건너뛴다.
                if (!HasTarget(step))
                {
                    Advance(step);
                }

                return;
            }

            _showing = step;
            _pressInside = false;
            _guardUntil = Time.unscaledTime + InputGuard;
            if (_session != null)
            {
                _session.HoldTimer(true);
            }

            // 1·4단계는 바늘이 움직이거나 스와이프로 잠수해야 하므로 제한 시간만 멈추고, 나머지는 게임 전체를 멈춘다.
            if (step != Step.Aim && step != Step.DiveSwipe && !_pausedGame)
            {
                _scaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
                _pausedGame = true;
            }

            // 4단계는 패널 밖 터치를 게임으로 그대로 보낸다(아래 스와이프로 잠수).
            var blockOutside = step != Step.DiveSwipe;
            for (var i = 0; i < _dims.Length; i++)
            {
                _dims[i].raycastTarget = blockOutside;
            }

            Fill(step);
            _root.gameObject.SetActive(true);
            // 비활성 상태에서 켠 overrideSorting은 풀릴 수 있어 켤 때마다 다시 넣는다.
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = _sortingOrder;
            ApplyHole(hole);
            PlaceCard(hole, step == Step.Aim || step == Step.DiveSwipe);
            PlaceArrow(step, hole);
            StartLoops();
            StartMotion(step, hole);
            SetActive(_blocker, true);
            if (_button != null)
            {
                _button.interactable = false;
            }

            _group.blocksRaycasts = true;
            _fade?.Kill();
            _group.alpha = 0f;
            _fade = _group.DOFade(1f, FadeTime).SetUpdate(true).SetLink(_root.gameObject);
        }

        void Complete()
        {
            var step = _showing;
            Hide(true, true);
            Advance(step);
        }

        void Advance(Step step)
        {
            switch (step)
            {
                case Step.Aim:
                    _next = Step.Steer;
                    break;
                case Step.Steer:
                    _next = Step.Timer;
                    break;
                case Step.Timer:
                    _next = Step.DiveSwipe;
                    Save(BasicDone);
                    break;
                case Step.DiveSwipe:
                    // 잠수에 들어가면 튜토리얼 완료로 저장한다. 조이스틱 안내는 이번 잠수에서만 이어서 보여 준다.
                    _next = Step.Swim;
                    Save(Done);
                    break;
                case Step.Swim:
                    _next = Step.None;
                    Save(Done);
                    break;
            }
        }

        static void Save(int value)
        {
            if (PlayerPrefs.GetInt(Key, 0) >= value)
            {
                return;
            }

            PlayerPrefs.SetInt(Key, value);
            PlayerPrefs.Save();
        }

        void Hide(bool fade, bool resume)
        {
            _showing = Step.None;
            ReleasePause(resume);
            _group.blocksRaycasts = false;
            SetActive(_blocker, false);
            _fade?.Kill();
            if (!fade)
            {
                Deactivate();
                return;
            }

            _fade = _group.DOFade(0f, FadeTime).SetUpdate(true).SetLink(_root.gameObject).OnComplete(Deactivate);
        }

        void Deactivate()
        {
            KillLoops();
            _group.alpha = 0f;
            _root.gameObject.SetActive(false);
        }

        void ReleasePause(bool resume)
        {
            if (_session != null)
            {
                _session.HoldTimer(false);
            }

            if (!_pausedGame)
            {
                return;
            }

            _pausedGame = false;
            if (resume && Time.timeScale <= 0f)
            {
                Time.timeScale = _scaleBefore;
            }
        }

        void KillLoops()
        {
            _motion?.Kill();
            _motion = null;
            for (var i = 0; i < _loops.Count; i++)
            {
                _loops[i]?.Kill();
            }

            _loops.Clear();
        }

        // 딤·패널·가림막을 누른 경우. 강조 구멍을 누른 입력은 원래대로 게임이 받는다.
        void HandleBackgroundTap()
        {
            if (_showing == Step.None || Time.unscaledTime < _guardUntil)
            {
                return;
            }

            if (_showing == Step.Aim)
            {
                _tapThrough?.Invoke();
            }
            else if (_showing == Step.Timer)
            {
                Complete();
            }
        }

        void HandleButton()
        {
            if (_showing == Step.None || Time.unscaledTime < _guardUntil)
            {
                return;
            }

            Complete();
        }

        // 강조 구멍 안에서 누른 손가락이 조금이라도 움직이면 "쓱" 한 것으로 본다. 그 움직임은 SteerReader가 그대로 갈고리 조종에 쓴다.
        bool DraggedInHole()
        {
            if (!PointerUtil.TryRead(out var position, out var pressed, out var released))
            {
                return false;
            }

            if (pressed)
            {
                _pressInside = InHole(position) && !PointerUtil.IsOverUi(position);
                _pressAt = position;
            }

            if (!_pressInside)
            {
                return false;
            }

            var moved = Vector2.Distance(FixedResolution.ToGamePixels(_pressAt), FixedResolution.ToGamePixels(position)) >= DragStartPixels;
            if (released)
            {
                _pressInside = false;
            }

            return moved;
        }

        // 안내가 뜬 뒤 새로 누른 손가락으로 조이스틱을 움직여야 넘어간다(뜨기 전부터 누르고 있던 손가락은 무시).
        bool JoystickMoved()
        {
            if (PointerUtil.TryRead(out _, out var pressed, out _) && pressed)
            {
                _pressInside = true;
            }

            return _pressInside && _stick != null && _stick.IsHeld && _stick.Value.sqrMagnitude >= 0.04f;
        }

        bool InHole(Vector2 screen)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, _uiCamera, out var local) && _hole.Contains(local);
        }

        void Fill(Step step)
        {
            var index = (int)step;
            _badge.text = Loc.F("ui.tutorial.tab", index, StepCount);
            for (var i = 0; i < _dots.Count; i++)
            {
                var on = i < index;
                if (_skin != null && _skin.dotOn != null && _skin.dotOff != null)
                {
                    _dots[i].sprite = on ? _skin.dotOn : _skin.dotOff;
                    _dots[i].color = Color.white;
                }
                else
                {
                    _dots[i].color = on ? FrameColor : new Color(0.4f, 0.42f, 0.48f, 1f);
                }
            }

            // 본문은 번역 표에서 줄바꿈(\n)째로 가져온다. {0}은 강조 색, 4단계의 {1}은 잠수 칸 수.
            string key;
            string button = null;
            var accent = Yellow;
            switch (step)
            {
                case Step.Aim:
                    key = "ui.tutorial.s1";
                    break;
                case Step.Steer:
                    key = "ui.tutorial.s2";
                    button = "ui.tutorial.s2.button";
                    break;
                case Step.Timer:
                    key = "ui.tutorial.s3";
                    button = "ui.tutorial.s3.button";
                    break;
                case Step.DiveSwipe:
                    key = "ui.tutorial.s4";
                    break;
                default:
                    key = "ui.tutorial.s5";
                    accent = Mint;
                    break;
            }

            var casts = _session != null ? _session.CastsPerDive : RunSession.BaseCastsPerDive;
            var body = Loc.F(key + ".body", accent, casts).Replace("\r", string.Empty);
            _title.text = Loc.T(key + ".title");
            _body.text = body;
            var bodyTop = PanelTop + TitleHeight + 6f;
            var bodyHeight = FitBody(body);
            _body.rectTransform.offsetMin = new Vector2(TextInset, -(bodyTop + bodyHeight));
            _body.rectTransform.offsetMax = new Vector2(-TextInset, -bodyTop);
            _cardHasButton = !string.IsNullOrEmpty(button);
            _button.gameObject.SetActive(_cardHasButton);
            if (_cardHasButton)
            {
                _buttonLabel.text = Loc.T(button);
            }

            // 버튼은 아래 테두리에 반쯤 걸치므로, 버튼 윗부분만큼 본문 아래를 비운다.
            _cardHeight = bodyTop + bodyHeight + (_cardHasButton ? ButtonHeight * 0.5f + 16f : 30f);
            _card.sizeDelta = new Vector2(_root.rect.width * PanelWidthRatio, _cardHeight);
        }

        // 본문 글자 크기를 고르고 본문 높이를 돌려준다. 영어처럼 줄이 길어 28px(갈무리14)로 한 줄에 안 들어가면
        // 도트가 깨지지 않는 22px(갈무리11)로 내린다. 그래도 넘치면 자동 줄바꿈된 실제 줄 수로 높이를 잡는다.
        float FitBody(string body)
        {
            var width = _root.rect.width * PanelWidthRatio - TextInset * 2f;
            var written = body.Split('\n').Length;
            var generator = new TextGenerator();
            var lines = written;
            var size = 28;
            foreach (var candidate in new[] { 28, 22 })
            {
                size = candidate;
                _body.font = UiFonts.Resolve(size);
                _body.fontSize = size;
                if (width <= 1f)
                {
                    break;
                }

                generator.Populate(body, _body.GetGenerationSettings(new Vector2(width, 10000f)));
                lines = Mathf.Max(written, generator.lineCount);
                if (lines <= written)
                {
                    break;
                }
            }

            return lines * BodyLine * size / 28f;
        }

        bool TryHole(Step step, out Rect hole)
        {
            hole = default;
            switch (step)
            {
                case Step.Aim:
                    return RectOf(_gauge != null ? _gauge.transform as RectTransform : null, 10f, out hole);
                case Step.Steer:
                {
                    var cam = Camera.main;
                    if (_hook == null || cam == null)
                    {
                        return false;
                    }

                    var center = WorldToLocal(cam, _hook.Position);
                    var bounds = _root.rect;
                    hole = Clip(Rect.MinMaxRect(bounds.xMin + 10f, center.y - SteerBandHalf, bounds.xMax - 10f, center.y + SteerBandHalf));
                    return hole.width > 1f && hole.height > 1f;
                }
                case Step.Timer:
                {
                    var found = false;
                    if (_hud != null && _hud.Panels != null)
                    {
                        found |= Encapsulate(_hud.Panels.TimeRect, ref hole, found);
                        found |= Encapsulate(_hud.Panels.Scrap, ref hole, found);
                    }

                    found |= Encapsulate(_chipPanel, ref hole, found);
                    if (found)
                    {
                        hole = Clip(Expand(hole, 8f));
                    }

                    return found;
                }
                case Step.DiveSwipe:
                {
                    // 오른쪽 위 잠수 칸(헬멧 칸) 전체.
                    var found = false;
                    if (_hud != null && _hud.Panels != null)
                    {
                        var cells = _hud.Panels.DiveCells;
                        for (var i = 0; i < cells.Count; i++)
                        {
                            found |= Encapsulate(cells[i] != null ? cells[i].rectTransform : null, ref hole, found);
                        }
                    }

                    if (found)
                    {
                        hole = Clip(Expand(hole, 8f));
                    }

                    return found;
                }
                case Step.Swim:
                    return RectOf(_stick != null ? _stick.Area : null, 18f, out hole);
            }

            return false;
        }

        bool HasTarget(Step step)
        {
            switch (step)
            {
                case Step.Aim:
                    return _gauge != null;
                case Step.Steer:
                    return _hook != null && Camera.main != null;
                case Step.Timer:
                    return (_hud != null && _hud.Panels != null) || _chipPanel != null;
                case Step.DiveSwipe:
                    return _hud != null && _hud.Panels != null && _hud.Panels.DiveCells.Count > 0;
                case Step.Swim:
                    return _stick != null && _stick.Area != null;
            }

            return false;
        }

        bool RectOf(RectTransform target, float padding, out Rect rect)
        {
            rect = default;
            if (!Encapsulate(target, ref rect, false))
            {
                return false;
            }

            rect = Clip(Expand(rect, padding));
            return true;
        }

        readonly Vector3[] _corners = new Vector3[4];

        bool Encapsulate(RectTransform target, ref Rect rect, bool hasRect)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                return false;
            }

            target.GetWorldCorners(_corners);
            var min = (Vector2)_root.InverseTransformPoint(_corners[0]);
            var max = min;
            for (var i = 1; i < 4; i++)
            {
                var p = (Vector2)_root.InverseTransformPoint(_corners[i]);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            if (hasRect)
            {
                min = Vector2.Min(min, rect.min);
                max = Vector2.Max(max, rect.max);
            }

            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        Vector2 WorldToLocal(Camera cam, Vector3 world)
        {
            var screen = RectTransformUtility.WorldToScreenPoint(cam, world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, _uiCamera, out var local);
            return local;
        }

        static Rect Expand(Rect rect, float padding)
        {
            return Rect.MinMaxRect(rect.xMin - padding, rect.yMin - padding, rect.xMax + padding, rect.yMax + padding);
        }

        Rect Clip(Rect rect)
        {
            var bounds = _root.rect;
            return Rect.MinMaxRect(
                Mathf.Clamp(rect.xMin, bounds.xMin, bounds.xMax),
                Mathf.Clamp(rect.yMin, bounds.yMin, bounds.yMax),
                Mathf.Clamp(rect.xMax, bounds.xMin, bounds.xMax),
                Mathf.Clamp(rect.yMax, bounds.yMin, bounds.yMax));
        }

        void ApplyHole(Rect hole)
        {
            _hole = hole;
            var bounds = _root.rect;
            var x0 = Mathf.InverseLerp(bounds.xMin, bounds.xMax, hole.xMin);
            var x1 = Mathf.InverseLerp(bounds.xMin, bounds.xMax, hole.xMax);
            var y0 = Mathf.InverseLerp(bounds.yMin, bounds.yMax, hole.yMin);
            var y1 = Mathf.InverseLerp(bounds.yMin, bounds.yMax, hole.yMax);
            Anchor(_dims[0].rectTransform, new Vector2(0f, y1), new Vector2(1f, 1f));
            Anchor(_dims[1].rectTransform, new Vector2(0f, 0f), new Vector2(1f, y0));
            Anchor(_dims[2].rectTransform, new Vector2(0f, y0), new Vector2(x0, y1));
            Anchor(_dims[3].rectTransform, new Vector2(x1, y0), new Vector2(1f, y1));
            if (_frame != null)
            {
                _frame.anchoredPosition = hole.center;
                _frame.sizeDelta = hole.size + new Vector2(8f, 8f);
            }

            if (_glow != null)
            {
                // 빛은 테두리만(가운데 비움) 구멍 바깥쪽으로 번지게 둔다.
                var spill = _glow.sprite != null ? _glow.sprite.border.x / _glow.pixelsPerUnitMultiplier : 20f;
                var glowRect = _glow.rectTransform;
                glowRect.anchoredPosition = hole.center;
                glowRect.sizeDelta = hole.size + new Vector2(spill * 2f, spill * 2f);
            }

            PlaceBrackets();
        }

        // 꺾쇠 4개: 왼쪽 위 그림을 0/-90/180/90도 돌려 구멍 네 모서리에 놓고, _spread(0~1)만큼 바깥으로 벌린다.
        void PlaceBrackets()
        {
            if (_brackets[0] == null)
            {
                return;
            }

            var offset = BracketSpread * (1f + _spread);
            _brackets[0].anchoredPosition = new Vector2(_hole.xMin - offset, _hole.yMax + offset);
            _brackets[1].anchoredPosition = new Vector2(_hole.xMax + offset, _hole.yMax + offset);
            _brackets[2].anchoredPosition = new Vector2(_hole.xMax + offset, _hole.yMin - offset);
            _brackets[3].anchoredPosition = new Vector2(_hole.xMin - offset, _hole.yMin - offset);
        }

        // 강조 구멍 위·아래 중 넓은 쪽에 패널을 둔다. pinFar면 손가락 자리를 남기도록 구멍에서 먼 끝에 붙인다.
        // 자리가 모자라면 화면 안에 들어가는 쪽으로만 민다. 탭(위)·버튼(아래)이 튀어나온 만큼도 자리에 넣는다.
        void PlaceCard(Rect hole, bool pinFar)
        {
            var bounds = _root.rect;
            var tabOver = TabHeight * 0.5f;
            var buttonOver = _cardHasButton ? ButtonHeight * 0.5f : 0f;
            var total = _cardHeight + tabOver + buttonOver;
            var above = bounds.yMax - hole.yMax;
            var below = hole.yMin - bounds.yMin;
            var placeBelow = below >= above;
            float lo;
            float hi;
            if (placeBelow)
            {
                lo = bounds.yMin + ScreenMargin;
                hi = hole.yMin - ScreenMargin;
            }
            else
            {
                lo = hole.yMax + ScreenMargin;
                hi = bounds.yMax - ScreenMargin;
            }

            float center;
            if (hi - lo < total)
            {
                center = placeBelow ? hi - total * 0.5f : lo + total * 0.5f;
            }
            else if (pinFar)
            {
                center = placeBelow ? lo + total * 0.5f : hi - total * 0.5f;
            }
            else
            {
                center = (lo + hi) * 0.5f;
            }

            center = Mathf.Clamp(center, bounds.yMin + ScreenMargin + total * 0.5f, bounds.yMax - ScreenMargin - total * 0.5f);
            var cardY = center - (tabOver - buttonOver) * 0.5f;
            _card.anchoredPosition = new Vector2(0f, cardY);
            var width = _card.sizeDelta.x;
            _cardBounds = Rect.MinMaxRect(-width * 0.5f, cardY - _cardHeight * 0.5f - buttonOver, width * 0.5f, cardY + _cardHeight * 0.5f + tabOver);
        }

        // 패널에서 강조 대상 쪽(위 또는 아래)을 가리키는 화살표. 4단계는 손가락 옆 아래 화살표가 대신한다.
        void PlaceArrow(Step step, Rect hole)
        {
            if (_arrow == null)
            {
                return;
            }

            var show = step != Step.DiveSwipe;
            _arrow.gameObject.SetActive(show);
            if (!show)
            {
                return;
            }

            var cardBelow = _cardBounds.center.y < hole.center.y;
            var x = Mathf.Clamp(hole.center.x, _cardBounds.xMin + 40f, _cardBounds.xMax - 40f);
            var edgeCard = cardBelow ? _cardBounds.yMax : _cardBounds.yMin;
            var edgeHole = cardBelow ? hole.yMin : hole.yMax;
            _arrowDir = cardBelow ? Vector2.up : Vector2.down;
            _arrowBase = new Vector2(x, (edgeCard + edgeHole) * 0.5f);
            _arrow.localEulerAngles = new Vector3(0f, 0f, cardBelow ? 90f : -90f);
            _arrow.anchoredPosition = _arrowBase;
        }

        void StartLoops()
        {
            KillLoops();
            _spread = 0f;
            if (_brackets[0] != null)
            {
                // 0.8초 주기로 살짝 벌어졌다 돌아온다.
                _loops.Add(DOTween.To(v =>
                    {
                        _spread = v;
                        PlaceBrackets();
                    }, 0f, 1f, 0.4f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true)
                    .SetLink(_root.gameObject));
            }

            if (_frame != null)
            {
                _frame.localScale = Vector3.one;
                _loops.Add(_frame.DOScale(1.05f, 0.55f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(_frame.gameObject));
            }

            if (_glow != null)
            {
                var color = _glow.color;
                color.a = 0.25f;
                _glow.color = color;
                _loops.Add(_glow.DOFade(0.45f, 0.8f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(_glow.gameObject));
            }

            if (_arrow != null && _arrow.gameObject.activeSelf)
            {
                _loops.Add(DOTween.To(v => _arrow.anchoredPosition = _arrowBase + _arrowDir * (ArrowNudge * v), 0f, 1f, 0.35f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true)
                    .SetLink(_arrow.gameObject));
            }
        }

        void StartMotion(Step step, Rect hole)
        {
            _motion?.Kill();
            var motion = step == Step.Aim ? FingerMotion.Tap
                : step == Step.Steer ? FingerMotion.Swipe
                : step == Step.DiveSwipe ? FingerMotion.SwipeDown
                : step == Step.Swim ? FingerMotion.Circle
                : FingerMotion.None;
            _finger.gameObject.SetActive(motion != FingerMotion.None);
            for (var i = 0; i < _rings.Length; i++)
            {
                _rings[i].gameObject.SetActive(motion == FingerMotion.Tap);
            }

            if (_arrowDown != null)
            {
                _arrowDown.gameObject.SetActive(motion == FingerMotion.SwipeDown);
            }

            _finger.localScale = Vector3.one;
            if (motion == FingerMotion.None)
            {
                return;
            }

            // 패널이 차지하지 않은 쪽 화면 가운데(탭·아래 스와이프 안내 자리).
            var bounds = _root.rect;
            var cardBelow = _cardBounds.center.y < 0f;
            var freeLo = cardBelow ? _cardBounds.yMax : bounds.yMin;
            var freeHi = cardBelow ? bounds.yMax : _cardBounds.yMin;
            if (step == Step.DiveSwipe)
            {
                freeLo = cardBelow ? _cardBounds.yMax : hole.yMax;
                freeHi = cardBelow ? hole.yMin : _cardBounds.yMin;
            }

            var free = new Vector2(0f, Mathf.Clamp(0f, freeLo + 80f, Mathf.Max(freeLo + 80f, freeHi - 80f)));
            if (step == Step.DiveSwipe)
            {
                free.y = (freeLo + freeHi) * 0.5f;
            }

            _motion = DOTween.Sequence().SetUpdate(true).SetLink(_finger.gameObject).SetLoops(-1);
            switch (motion)
            {
                case FingerMotion.Tap:
                {
                    // 손가락이 들렸다가 톡 누르고, 누른 자리에서 링 2개가 0.4초 차이로 퍼진다.
                    _tapOrigin = free;
                    _finger.anchoredPosition = free;
                    for (var i = 0; i < _rings.Length; i++)
                    {
                        _rings[i].anchoredPosition = free;
                    }

                    RingsAt(0f);
                    _motion.Append(_finger.DOAnchorPos(free + new Vector2(0f, 22f), 0.3f).SetEase(Ease.OutSine));
                    _motion.Append(_finger.DOAnchorPos(free, TapPress).SetEase(Ease.InQuad));
                    _motion.Join(_finger.DOScale(0.86f, TapPress));
                    _motion.Append(_finger.DOScale(1f, 0.14f));
                    _motion.AppendInterval(0.3f);
                    // 링은 한 바퀴 전체를 시간값 하나로 그려서, 반복할 때 되감겨도 어긋나지 않는다.
                    _motion.Insert(0f, DOTween.To(RingsAt, 0f, TapCycle, TapCycle).SetEase(Ease.Linear));
                    break;
                }
                case FingerMotion.Swipe:
                {
                    var origin = hole.center;
                    if (_hook != null && Camera.main != null)
                    {
                        origin = WorldToLocal(Camera.main, _hook.Position);
                    }

                    var left = origin + new Vector2(-90f, -12f);
                    var right = origin + new Vector2(90f, -12f);
                    _finger.anchoredPosition = left;
                    _motion.Append(_finger.DOAnchorPos(right, 0.6f).SetEase(Ease.InOutSine));
                    _motion.Append(_finger.DOAnchorPos(left, 0.6f).SetEase(Ease.InOutSine));
                    break;
                }
                case FingerMotion.SwipeDown:
                {
                    // 손가락이 1초에 걸쳐 위에서 아래로 쓸어내리고, 옆에 아래 화살표가 까딱인다.
                    var top = free + new Vector2(-24f, 70f);
                    var bottom = free + new Vector2(-24f, -70f);
                    _finger.anchoredPosition = top;
                    _motion.Append(_finger.DOAnchorPos(bottom, 1f).SetEase(Ease.InOutSine));
                    if (_arrowDown != null)
                    {
                        var arrowBase = free + new Vector2(52f, 0f);
                        _arrowDown.anchoredPosition = arrowBase;
                        _motion.Insert(0f, DOTween.To(v => _arrowDown.anchoredPosition = arrowBase + Vector2.down * (ArrowNudge * 2f * Mathf.Sin(v * Mathf.PI)), 0f, 1f, 1f));
                    }

                    break;
                }
                case FingerMotion.Circle:
                {
                    const float radius = 42f;
                    var origin = hole.center;
                    _finger.anchoredPosition = origin + new Vector2(0f, -radius);
                    _motion.Append(_finger.DOAnchorPos(origin + new Vector2(radius, 0f), 0.28f).SetEase(Ease.Linear));
                    _motion.Append(_finger.DOAnchorPos(origin + new Vector2(0f, radius), 0.28f).SetEase(Ease.Linear));
                    _motion.Append(_finger.DOAnchorPos(origin + new Vector2(-radius, 0f), 0.28f).SetEase(Ease.Linear));
                    _motion.Append(_finger.DOAnchorPos(origin + new Vector2(0f, -radius), 0.28f).SetEase(Ease.Linear));
                    break;
                }
            }
        }

        // 손가락이 바닥에 닿는 순간(TapDown)부터 첫 링, 0.4초 뒤 둘째 링이 커지며 사라진다.
        void RingsAt(float t)
        {
            for (var i = 0; i < _rings.Length; i++)
            {
                var phase = Mathf.Repeat(t - TapDown - RingGap * i, TapCycle);
                var p = Mathf.Clamp01(phase / TapRing);
                var visible = phase < TapRing;
                _rings[i].localScale = Vector3.one * Mathf.Lerp(0.4f, 1.5f, 1f - (1f - p) * (1f - p));
                _ringImages[i].color = new Color(FrameColor.r, FrameColor.g, FrameColor.b, visible ? 0.9f * (1f - p) : 0f);
            }
        }

        void Build(RectTransform parent)
        {
            _skin = TutorialSkin.Load();
            var rootCanvas = parent.GetComponentInParent<Canvas>();
            if (rootCanvas != null)
            {
                rootCanvas = rootCanvas.rootCanvas;
            }

            _uiCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;
            _sortingOrder = (rootCanvas != null ? rootCanvas.sortingOrder : 0) + SortingAbove;

            var go = new GameObject("FirstPlayGuide", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            _root = (RectTransform)go.transform;
            Anchor(_root, Vector2.zero, Vector2.one);
            _root.pivot = new Vector2(0.5f, 0.5f);
            // 달러 패널(UiStayOnTop)·HUD보다 위에 그리려고 따로 Canvas를 둔다. 터치를 받으려면 GraphicRaycaster도 필요하다.
            _canvas = go.AddComponent<Canvas>();
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = _sortingOrder;
            go.AddComponent<GraphicRaycaster>();
            _group = go.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;

            _dims[0] = Dim("DimTop");
            _dims[1] = Dim("DimBottom");
            _dims[2] = Dim("DimLeft");
            _dims[3] = Dim("DimRight");

            BuildHighlight();

            _arrow = SkinImage("Arrow", _root, SkinSprite(s => s.arrowRight), 46f);
            _arrowDown = SkinImage("ArrowDown", _root, SkinSprite(s => s.arrowDown), 34f);

            for (var i = 0; i < _rings.Length; i++)
            {
                _rings[i] = NewRect("TapRing" + i, _root);
                _rings[i].sizeDelta = new Vector2(RingSize, RingSize);
                _ringImages[i] = _rings[i].gameObject.AddComponent<Image>();
                _ringImages[i].sprite = SkinSprite(s => s.ring) ?? RingSprite();
                _ringImages[i].preserveAspect = true;
                _ringImages[i].raycastTarget = false;
            }

            BuildFinger();

            // 단계가 뜬 직후(입력 무시 시간)와 3단계에서 구멍까지 덮는 투명 가림막.
            var blocker = NewRect("InputGuard", _root);
            Anchor(blocker, Vector2.zero, Vector2.one);
            var blockerImage = blocker.gameObject.AddComponent<Image>();
            blockerImage.color = new Color(0f, 0f, 0f, 0f);
            blocker.gameObject.AddComponent<TutorialTapCatcher>().Clicked += HandleBackgroundTap;
            _blocker = blocker.gameObject;

            BuildCard();
            go.SetActive(false);
        }

        Sprite SkinSprite(Func<TutorialSkin, Sprite> pick)
        {
            return _skin != null ? pick(_skin) : null;
        }

        Image Dim(string name)
        {
            var rect = NewRect(name, _root);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = DimColor;
            image.raycastTarget = true;
            rect.gameObject.AddComponent<TutorialTapCatcher>().Clicked += HandleBackgroundTap;
            return image;
        }

        // 스킨이 있으면 은은한 빛 + 꺾쇠 4개, 없으면 예전 노란 사각형 테두리.
        void BuildHighlight()
        {
            var glow = SkinSprite(s => s.glow);
            if (glow != null)
            {
                var glowRect = NewRect("Glow", _root);
                _glow = glowRect.gameObject.AddComponent<Image>();
                _glow.sprite = glow;
                _glow.type = Image.Type.Sliced;
                _glow.fillCenter = false;
                _glow.color = new Color(FrameColor.r, FrameColor.g, FrameColor.b, 0.25f);
                _glow.raycastTarget = false;
            }

            var bracket = SkinSprite(s => s.bracket);
            if (bracket == null)
            {
                _frame = NewRect("Spotlight", _root);
                var frameImage = _frame.gameObject.AddComponent<Image>();
                frameImage.sprite = FrameSprite();
                frameImage.type = Image.Type.Sliced;
                frameImage.color = FrameColor;
                frameImage.raycastTarget = false;
                return;
            }

            var height = BracketWidth * bracket.rect.height / Mathf.Max(1f, bracket.rect.width);
            var angles = new[] { 0f, -90f, 180f, 90f };
            for (var i = 0; i < _brackets.Length; i++)
            {
                var rect = NewRect("Bracket" + i, _root);
                rect.sizeDelta = new Vector2(BracketWidth, height);
                // 꺾쇠 꼭짓점(왼쪽 위)을 피벗으로 두고 돌리면 네 모서리 모양이 된다.
                rect.pivot = new Vector2(0f, 1f);
                rect.localEulerAngles = new Vector3(0f, 0f, angles[i]);
                var image = rect.gameObject.AddComponent<Image>();
                image.sprite = bracket;
                image.raycastTarget = false;
                _brackets[i] = rect;
            }
        }

        void BuildFinger()
        {
            _finger = NewRect("Finger", _root);
            var image = _finger.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            var hand = SkinSprite(s => s.hand);
            if (hand != null)
            {
                image.sprite = hand;
                _finger.sizeDelta = new Vector2(HandWidth, HandWidth * hand.rect.height / Mathf.Max(1f, hand.rect.width));
                // tut_hand의 검지 끝(왼쪽 위)이 가리키는 점이 되게 피벗을 둔다.
                _finger.pivot = new Vector2(0.15f, 0.94f);
                return;
            }

            _finger.sizeDelta = new Vector2(54f, 72f);
            // 손가락 끝(스프라이트 위쪽)이 가리키는 점이 되게 피벗을 위로 올린다.
            _finger.pivot = new Vector2(0.5f, 0.92f);
            image.sprite = FingerSprite();
            image.color = Color.white;
            var outline = _finger.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        // 스프라이트가 있으면 비율을 지킨 Simple 이미지로, 없으면 null(그 장식은 생략).
        static RectTransform SkinImage(string name, RectTransform parent, Sprite sprite, float width)
        {
            if (sprite == null)
            {
                return null;
            }

            var rect = NewRect(name, parent);
            rect.sizeDelta = new Vector2(width, width * sprite.rect.height / Mathf.Max(1f, sprite.rect.width));
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            rect.gameObject.SetActive(false);
            return rect;
        }

        static void Slice(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = SkinBorderScale;
            image.color = Color.white;
        }

        void BuildCard()
        {
            _card = NewRect("Card", _root);
            _card.sizeDelta = new Vector2(_root.rect.width * PanelWidthRatio, 260f);
            var image = _card.gameObject.AddComponent<Image>();
            var panel = SkinSprite(s => s.panel);
            if (panel != null)
            {
                Slice(image, panel);
            }
            else
            {
                image.color = CardColor;
                var outline = _card.gameObject.AddComponent<Outline>();
                outline.effectColor = FrameColor;
                outline.effectDistance = new Vector2(3f, -3f);
            }

            _card.gameObject.AddComponent<TutorialTapCatcher>().Clicked += HandleBackgroundTap;

            // 제목 탭: 패널 왼쪽 위 테두리에 반쯤 걸친다.
            var tab = NewRect("Tab", _card);
            tab.anchorMin = new Vector2(0f, 1f);
            tab.anchorMax = new Vector2(0f, 1f);
            tab.pivot = new Vector2(0f, 0.5f);
            tab.anchoredPosition = new Vector2(TextInset - 6f, 0f);
            tab.sizeDelta = new Vector2(TabWidth, TabHeight);
            var tabImage = tab.gameObject.AddComponent<Image>();
            tabImage.raycastTarget = false;
            var tabSprite = SkinSprite(s => s.tab);
            if (tabSprite != null)
            {
                Slice(tabImage, tabSprite);
            }
            else
            {
                tabImage.color = FrameColor;
            }

            _badge = UiFonts.CreateText(tab, "Label", 22, TextAnchor.MiddleCenter);
            Anchor(_badge.rectTransform, Vector2.zero, Vector2.one);
            _badge.color = tabSprite != null ? FrameColor : new Color(0.08f, 0.06f, 0.02f, 1f);

            _title = UiFonts.CreateText(_card, "Title", 28, TextAnchor.MiddleCenter);
            _title.rectTransform.anchorMin = new Vector2(0f, 1f);
            _title.rectTransform.anchorMax = new Vector2(1f, 1f);
            _title.rectTransform.offsetMin = new Vector2(TextInset, -(PanelTop + TitleHeight));
            _title.rectTransform.offsetMax = new Vector2(-TextInset, -PanelTop);
            _title.color = FrameColor;

            _body = UiFonts.CreateText(_card, "Body", 28, TextAnchor.UpperCenter);
            _body.rectTransform.anchorMin = new Vector2(0f, 1f);
            _body.rectTransform.anchorMax = new Vector2(1f, 1f);
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            _body.lineSpacing = 1.05f;
            _body.supportRichText = true;
            _body.color = Color.white;

            // 단계 점: 패널 안 오른쪽 아래.
            var dots = NewRect("Dots", _card);
            dots.anchorMin = new Vector2(1f, 0f);
            dots.anchorMax = new Vector2(1f, 0f);
            dots.pivot = new Vector2(1f, 0f);
            dots.anchoredPosition = new Vector2(-TextInset, 20f);
            dots.sizeDelta = new Vector2(StepCount * DotSize + (StepCount - 1) * DotGap, DotSize);
            var dotSprite = SkinSprite(s => s.dotOff);
            for (var i = 0; i < StepCount; i++)
            {
                var dot = NewRect("Dot" + i, dots);
                dot.anchorMin = new Vector2(0f, 0.5f);
                dot.anchorMax = new Vector2(0f, 0.5f);
                dot.pivot = new Vector2(0f, 0.5f);
                dot.anchoredPosition = new Vector2(i * (DotSize + DotGap), 0f);
                dot.sizeDelta = new Vector2(DotSize, DotSize);
                var dotImage = dot.gameObject.AddComponent<Image>();
                dotImage.sprite = dotSprite != null ? dotSprite : PlaceholderFactory.Circle(Color.white, 16);
                dotImage.preserveAspect = true;
                dotImage.raycastTarget = false;
                _dots.Add(dotImage);
            }

            // 확인 버튼: 패널 아래 테두리 가운데에 반쯤 걸친다.
            _button = UiButton.Create(_card, "Confirm", string.Empty, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), HandleButton);
            var buttonRect = (RectTransform)_button.transform;
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            buttonRect.anchoredPosition = Vector2.zero;
            _buttonLabel = _button.GetComponentInChildren<Text>();
            var buttonImage = _button.GetComponent<Image>();
            var buttonSprite = SkinSprite(s => s.button);
            if (buttonImage != null && buttonSprite != null)
            {
                Slice(buttonImage, buttonSprite);
                var outline = _button.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.enabled = false;
                }
            }
            else if (buttonImage != null)
            {
                buttonImage.color = new Color(0.12f, 0.36f, 0.3f, 1f);
            }

            if (_buttonLabel != null)
            {
                _buttonLabel.color = Color.white;
            }
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        static void Anchor(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }

        // 스킨이 없을 때 쓰는 가장자리 4px만 칠한 9-slice 테두리.
        static Sprite FrameSprite()
        {
            const int size = 16;
            const int edge = 4;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var onEdge = x < edge || y < edge || x >= size - edge || y >= size - edge;
                    pixels[y * size + x] = onEdge ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }

            return MakeSprite(pixels, size, size, new Vector2(0.5f, 0.5f), new Vector4(edge, edge, edge, edge));
        }

        static Sprite RingSprite()
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5f - size * 0.5f;
                    var dy = y + 0.5f - size * 0.5f;
                    var d = Mathf.Sqrt(dx * dx + dy * dy);
                    var a = Mathf.Clamp01(1f - Mathf.Abs(d - 27f) / 4f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            return MakeSprite(pixels, size, size, new Vector2(0.5f, 0.5f), Vector4.zero);
        }

        static Sprite FingerSprite()
        {
            const int w = 24;
            const int h = 32;
            var pixels = new Color32[w * h];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var nx = (x + 0.5f) / w;
                    var ny = (y + 0.5f) / h;
                    var pad = ny < 0.42f && nx > 0.28f && nx < 0.72f && ny > 0.05f;
                    var tip = (nx - 0.5f) * (nx - 0.5f) / 0.07f + (ny - 0.62f) * (ny - 0.62f) / 0.16f < 1f && ny > 0.34f;
                    pixels[y * w + x] = pad || tip
                        ? new Color32(255, 255, 255, 240)
                        : new Color32(0, 0, 0, 0);
                }
            }

            return MakeSprite(pixels, w, h, new Vector2(0.5f, 0.2f), Vector4.zero);
        }

        static Sprite MakeSprite(Color32[] pixels, int w, int h, Vector2 pivot, Vector4 border)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, w, h), pivot, 100f, 0, SpriteMeshType.FullRect, border);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
