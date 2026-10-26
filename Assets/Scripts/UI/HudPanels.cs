using System.Collections.Generic;
using DG.Tweening;
using ScrapFishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    // 인게임 HUD 패널(보트·잠수 공통): 왼쪽 위 스크랩, 오른쪽 위 TIME·잠수 칸, 오른쪽 아래 수심(+구역 태그).
    // 스킨은 Resources/UI/HudSkin. 크기는 480×854 기준 픽셀에 hudScale을 곱해 정하고, 패널은 원본 비율 그대로 줄여 쓴다(Simple + Preserve Aspect).
    // 위치(480×854, 왼쪽 아래 원점): 스크랩은 칩 패널(아래 끝 y=770) 아래 6px, TIME·잠수 칸은 톱니 버튼(아래 끝 y=816) 아래,
    // 수심 패널은 수심 게이지 틀(아래 끝 약 y=149) 아래 4px에 오른쪽 맞춤. 보트·낚시꾼(y 약 410~590), 낚싯줄과 겹치지 않는다.
    public sealed class HudPanels
    {
        public const int WarningSeconds = 10;

        const float LeftX = 10f;
        // 칩 패널 아래 끝(위에서 84px) + 6px.
        const float ScrapTop = 90f;
        const float RightMargin = 5f;
        // 수심 패널 위 끝(아래에서). 수심 게이지 틀 아래 끝(약 149) - 4px.
        const float DepthTopFromBottom = 145f;
        const float RightTop = 41f;
        const float DepthWidth = 124f;
        const float ZoneWidth = 88f;
        const float ZoneOffsetX = 14f;
        const float ZoneOverlap = 1f;
        const float ScrapWidth = 112f;
        const float TimeWidth = 86f;
        const float CellWidth = 34f;
        const float CellHeight = 33f;
        const float CellGap = 3f;
        const float DiveBlinkHalf = 0.4f;

        static readonly Color TimeColor = Palette.Cyan;
        static readonly Color WarningColor = new Color(1f, 0.32f, 0.32f);
        static readonly Color DepthColor = Palette.NeonGreen;
        static readonly Color ZoneColor = new Color(0.93f, 0.84f, 1f);
        static readonly Color ScrapColor = Palette.Gold;
        static readonly Color ScrapFullColor = Palette.NeonGreen;

        readonly HudSkin _skin;
        readonly float _scale;
        readonly RectTransform _cellRoot;
        readonly List<Image> _cells = new List<Image>();
        int _lastSeconds = int.MinValue;
        int _lastMeters = int.MinValue;
        string _lastZone;
        bool _diveBlinking;

        public RectTransform Depth { get; }
        public RectTransform Zone { get; }
        public RectTransform Scrap { get; }
        public RectTransform TimeRect { get; }
        public Image TimeImage { get; }
        public Text TimeText { get; }
        public Text DepthText { get; }
        public Text ZoneText { get; }
        public Image ScrapImage { get; }
        public Text ScrapText { get; }
        public Image ScrapFill { get; }
        public IReadOnlyList<Image> DiveCells => _cells;
        public bool Warning { get; private set; }
        public bool Full { get; private set; }
        public bool DiveBlinking => _diveBlinking;

        public HudPanels(RectTransform parent, HudSkin skin)
        {
            _skin = skin;
            _scale = skin != null ? Mathf.Max(0.1f, skin.hudScale) : 1f;

            var depthHeight = Height(DepthWidth, Sprite(s => s.depthPanel), 70f / 203f);
            var zoneHeight = Height(ZoneWidth, Sprite(s => s.zoneTag), 33f / 144f);
            var scrapHeight = Height(ScrapWidth, Sprite(s => s.scrapPanel), 89f / 182f);
            var timeHeight = Height(TimeWidth, Sprite(s => s.timePanel), 51f / 144f);

            // 왼쪽 위: 스크랩(칩 패널 아래).
            Scrap = Panel(parent, "ScrapPanel", Sprite(s => s.scrapPanel), new Vector2(0f, 1f), new Vector2(LeftX, -ScrapTop), ScrapWidth * _scale, scrapHeight);

            // 오른쪽 아래: 수심 → 구역 태그(패널 아래 끝에 1px 겹침, 패널 왼쪽에서 14px 안쪽).
            Depth = Panel(parent, "DepthPanel", Sprite(s => s.depthPanel), new Vector2(1f, 0f), new Vector2(-RightMargin, DepthTopFromBottom), DepthWidth * _scale, depthHeight,
                new Vector2(1f, 1f));
            var zoneRight = RightMargin + (DepthWidth - ZoneOffsetX - ZoneWidth) * _scale;
            var zoneTop = DepthTopFromBottom - depthHeight + ZoneOverlap * _scale;
            Zone = Panel(parent, "ZoneTag", Sprite(s => s.zoneTag), new Vector2(1f, 0f), new Vector2(-zoneRight, zoneTop), ZoneWidth * _scale, zoneHeight,
                new Vector2(1f, 1f));
            // 구역 태그는 수심 패널 뒤(위 끝이 패널 아래에 걸리게).
            Zone.SetSiblingIndex(Depth.GetSiblingIndex());

            // 오른쪽 열: TIME → 잠수 칸(세로).
            TimeRect = Panel(parent, "TimePanel", Sprite(s => s.timePanel), new Vector2(1f, 1f), new Vector2(-RightMargin, -RightTop), TimeWidth * _scale, timeHeight);
            _cellRoot = new GameObject("DiveCells", typeof(RectTransform)).GetComponent<RectTransform>();
            _cellRoot.SetParent(parent, false);
            _cellRoot.anchorMin = _cellRoot.anchorMax = _cellRoot.pivot = new Vector2(1f, 1f);
            _cellRoot.anchoredPosition = new Vector2(-RightMargin, -(RightTop + timeHeight + CellGap * _scale));
            _cellRoot.sizeDelta = new Vector2(CellWidth * _scale, 0f);

            TimeImage = TimeRect.GetComponent<Image>();
            TimeText = Label(TimeRect, "Number", 18, TextAnchor.MiddleCenter, Area(s => s.timeNumber, Rect.MinMaxRect(0.354f, 0.157f, 0.951f, 0.824f)), TimeColor);
            DepthText = Label(Depth, "Number", 19, TextAnchor.MiddleRight, Area(s => s.depthNumber, Rect.MinMaxRect(0.305f, 0.171f, 0.837f, 0.8f)), DepthColor);
            ZoneText = Label(Zone, "Name", 13, TextAnchor.MiddleCenter, Area(s => s.zoneText, Rect.MinMaxRect(0.069f, 0.152f, 0.931f, 0.818f)), ZoneColor);
            // HUD 글꼴은 도트가 깨지지 않게 11의 배수(최소 22)로 맞춰진다. 20px 높이 태그에는 갈무리11 원래 크기(11)를 쓴다.
            ZoneText.fontSize = 11;
            ScrapImage = Scrap.GetComponent<Image>();
            ScrapText = Label(Scrap, "Number", 16, TextAnchor.MiddleCenter, Area(s => s.scrapNumber, Rect.MinMaxRect(0.341f, 0.315f, 0.951f, 0.899f)), ScrapColor);

            // 스크랩 진행 바: barArea 안에서 count/max 비율만큼 오른쪽으로 늘어난다(0이면 숨김).
            var barArea = new GameObject("BarArea", typeof(RectTransform)).GetComponent<RectTransform>();
            barArea.SetParent(Scrap, false);
            Anchor(barArea, Area(s => s.scrapBar, Rect.MinMaxRect(0.077f, 0.135f, 0.923f, 0.236f)));
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(barArea, false);
            ScrapFill = fill.GetComponent<Image>();
            ScrapFill.sprite = Sprite(s => s.scrapBarFill);
            ScrapFill.type = Image.Type.Sliced;
            ScrapFill.raycastTarget = false;
            ScrapFill.color = ScrapFill.sprite != null ? Color.white : ScrapColor;
            Anchor(ScrapFill.rectTransform, new Rect(0f, 0f, 0f, 1f));
            ScrapFill.enabled = false;
        }

        Sprite Sprite(System.Func<HudSkin, Sprite> pick)
        {
            return _skin != null ? pick(_skin) : null;
        }

        Rect Area(System.Func<HudSkin, Rect> pick, Rect fallback)
        {
            return _skin != null ? pick(_skin) : fallback;
        }

        // 폭 기준, 높이는 원본 비율 유지.
        float Height(float width, Sprite sprite, float fallbackRatio)
        {
            var ratio = sprite != null ? sprite.rect.height / sprite.rect.width : fallbackRatio;
            return width * _scale * ratio;
        }

        // corner: 붙일 화면 모서리(앵커). pivot을 따로 주지 않으면 같은 모서리를 기준점으로 쓴다.
        static RectTransform Panel(RectTransform parent, string name, Sprite sprite, Vector2 corner, Vector2 position, float width, float height, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = corner;
            rect.pivot = pivot ?? corner;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, height);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = sprite != null ? Color.white : new Color(0.06f, 0.08f, 0.14f, 0.85f);
            return rect;
        }

        Text Label(RectTransform panel, string name, int size, TextAnchor anchor, Rect area, Color color)
        {
            var text = UiFonts.CreateText(panel, name, Mathf.RoundToInt(size * _scale), anchor);
            Anchor(text.rectTransform, area);
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        static void Anchor(RectTransform rect, Rect area)
        {
            rect.anchorMin = new Vector2(area.xMin, area.yMin);
            rect.anchorMax = new Vector2(area.xMax, area.yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // 남은 초만 표시("56"). 10초 이하면 빨간 패널·빨간 숫자, 초가 바뀔 때마다 살짝 깜빡인다.
        public void SetTime(int seconds)
        {
            if (seconds == _lastSeconds)
            {
                return;
            }

            _lastSeconds = seconds;
            TimeText.text = seconds >= 0 ? seconds.ToString() : string.Empty;
            Warning = seconds >= 0 && seconds <= WarningSeconds;
            var sprite = Warning ? Sprite(s => s.timePanelWarning) : Sprite(s => s.timePanel);
            if (sprite != null)
            {
                TimeImage.sprite = sprite;
            }

            TimeText.DOKill(false);
            TimeText.color = Warning ? WarningColor : TimeColor;
            if (Warning && seconds > 0)
            {
                var c = TimeText.color;
                TimeText.color = new Color(c.r, c.g, c.b, 0.35f);
                TimeText.DOFade(1f, 0.3f).SetUpdate(true).SetLink(TimeText.gameObject);
            }
        }

        // 수심 숫자만(단위 m는 그림에 있다). 구역 이름이 바뀔 때만 태그를 고친다.
        public void SetDepth(float normalized)
        {
            ShowDepth(DepthZone.Meters(normalized), DepthZone.Name(normalized));
        }

        // 잠수 중: 잠수부 위치로 1500~1600m, 구역은 "해저 평원".
        public void SetDiveDepth(float normalized)
        {
            ShowDepth(DepthZone.DiveMeters(normalized), DepthZone.DiveZoneName);
        }

        void ShowDepth(float depthMeters, string zone)
        {
            var meters = Mathf.RoundToInt(depthMeters);
            if (meters != _lastMeters)
            {
                _lastMeters = meters;
                DepthText.text = meters.ToString();
            }

            if (zone != _lastZone)
            {
                _lastZone = zone;
                ZoneText.text = zone;
            }
        }

        public void SetDepthVisible(bool visible)
        {
            SetActive(Depth, visible);
            SetActive(Zone, visible);
        }

        public void SetDiveCellsVisible(bool visible)
        {
            SetActive(_cellRoot, visible);
            if (!visible)
            {
                StopDiveBlink();
            }
        }

        public void SetScrapVisible(bool visible)
        {
            SetActive(Scrap, visible);
        }

        public void SetTimeVisible(bool visible)
        {
            SetActive(TimeRect, visible);
        }

        static void SetActive(Component target, bool visible)
        {
            if (target != null && target.gameObject.activeSelf != visible)
            {
                target.gameObject.SetActive(visible);
            }
        }

        // "0/10"과 진행 바. 가득 차면 초록 패널·초록 숫자.
        public void SetScrap(int count, int max)
        {
            var ratio = max > 0 ? Mathf.Clamp01(count / (float)max) : 0f;
            Full = max > 0 && count >= max;
            ScrapText.text = max > 0 ? count + "/" + max : string.Empty;
            ScrapText.color = Full ? ScrapFullColor : ScrapColor;
            var sprite = Full ? Sprite(s => s.scrapPanelFull) : Sprite(s => s.scrapPanel);
            if (sprite != null)
            {
                ScrapImage.sprite = sprite;
            }

            ScrapFill.enabled = ratio > 0f;
            ScrapFill.rectTransform.anchorMax = new Vector2(ratio, 1f);
            ScrapFill.rectTransform.offsetMax = Vector2.zero;
        }

        // CastsPerDive개 칸을 위에서 아래로 쌓고, DiveProgress만큼 채운다. 잠수 가능하면 채운 칸이 0.8초 주기로 깜빡인다.
        public void SetDive(int progress, int casts, bool canDive)
        {
            casts = Mathf.Max(0, casts);
            if (_cells.Count != casts)
            {
                RebuildCells(casts);
            }

            var filled = Mathf.Clamp(progress, 0, casts);
            for (var i = 0; i < _cells.Count; i++)
            {
                var sprite = i < filled ? Sprite(s => s.diveFull) : Sprite(s => s.diveEmpty);
                if (sprite != null)
                {
                    _cells[i].sprite = sprite;
                }
                else
                {
                    _cells[i].color = i < filled ? Palette.Cyan : new Color(0.4f, 0.45f, 0.5f, 0.6f);
                }
            }

            if (canDive && filled > 0)
            {
                StartDiveBlink(filled);
            }
            else
            {
                StopDiveBlink();
            }
        }

        void RebuildCells(int casts)
        {
            StopDiveBlink();
            foreach (var cell in _cells)
            {
                if (cell == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Object.Destroy(cell.gameObject);
                }
                else
                {
                    Object.DestroyImmediate(cell.gameObject);
                }
            }

            _cells.Clear();
            var w = CellWidth * _scale;
            var h = CellHeight * _scale;
            var gap = CellGap * _scale;
            for (var i = 0; i < casts; i++)
            {
                var go = new GameObject("Cell" + i, typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)go.transform;
                rect.SetParent(_cellRoot, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(0f, -i * (h + gap));
                rect.sizeDelta = new Vector2(w, h);
                var image = go.GetComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;
                _cells.Add(image);
            }

            _cellRoot.sizeDelta = new Vector2(w, casts > 0 ? casts * h + (casts - 1) * gap : 0f);
        }

        void StartDiveBlink(int filled)
        {
            if (_diveBlinking)
            {
                return;
            }

            _diveBlinking = true;
            for (var i = 0; i < filled && i < _cells.Count; i++)
            {
                var cell = _cells[i];
                cell.DOKill(false);
                cell.DOFade(0.55f, DiveBlinkHalf).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(cell.gameObject);
            }
        }

        void StopDiveBlink()
        {
            if (!_diveBlinking)
            {
                return;
            }

            _diveBlinking = false;
            foreach (var cell in _cells)
            {
                if (cell != null)
                {
                    cell.DOKill(false);
                    var c = cell.color;
                    cell.color = new Color(c.r, c.g, c.b, 1f);
                }
            }
        }
    }
}
