using System;
using DG.Tweening;
using ScrapFishing.Audio;
using ScrapFishing.Core;
using ScrapFishing.Data;
using ScrapFishing.FX;
using ScrapFishing.Scrap;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    public class ResultsView : MonoBehaviour
    {
        const float InputLock = 0.8f;

        GameObject _root;
        CanvasGroup _group;
        Image _dim;
        RectTransform _titleRect;
        Text _cyText;
        CountingText _cyCount;
        Text _bestText;
        Text _detail;
        Text _goalText;
        Text _zoneText;
        RectTransform _goalRect;
        Vector3 _goalScale = Vector3.one;
        RectTransform _lootRoot;
        GridLayoutGroup _lootGrid;
        bool _lootPlayed;
        GameObject _newBest;
        RectTransform _bestRect;
        Vector3 _bestScale = Vector3.one;
        Button _restart;
        RectTransform _restartRect;
        Action _onRestart;
        bool _restartArmed;
        float _restartAt;
        Vector3 _restartScale = Vector3.one;
        UiFxSettings _settings;
        Sequence _intro;
        float _readyAt;
        int _targetTotal;
        bool _counting;
        bool _countAudioDone;
        bool _pendingNewBest;
        RunSession _session;

        public void Build(Transform canvas, Action onRestart, Action onMarket = null)
        {
            _onRestart = onRestart;
            _settings = Resources.Load<UiFxSettings>("UiFxSettings");
            _root = new GameObject("Results");
            _root.transform.SetParent(canvas, false);
            var rect = _root.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var background = _root.AddComponent<Image>();
            background.color = new Color(0.02f, 0.03f, 0.06f, 0.84f);
            _dim = background;
            _group = _root.AddComponent<CanvasGroup>();

            var title = UiFonts.CreateText(rect, "Title", 28, TextAnchor.MiddleCenter);
            Stretch(title.rectTransform, new Vector2(0.08f, 0.86f), new Vector2(0.92f, 0.93f));
            title.text = "정산";
            _titleRect = title.rectTransform;

            var newBest = UiFonts.CreateText(rect, "NewBestText", 28, TextAnchor.MiddleCenter);
            Stretch(newBest.rectTransform, new Vector2(0.08f, 0.79f), new Vector2(0.92f, 0.86f));
            newBest.text = "NEW BEST!";
            newBest.color = Palette.Magenta;
            _newBest = newBest.gameObject;
            _bestRect = newBest.rectTransform;
            _bestScale = _bestRect.localScale;
            _newBest.SetActive(false);

            _cyText = UiFonts.CreateText(rect, "CyText", 28, TextAnchor.MiddleCenter);
            Stretch(_cyText.rectTransform, new Vector2(0.08f, 0.61f), new Vector2(0.92f, 0.68f));
            _cyText.color = Palette.Gold;
            _cyCount = _cyText.gameObject.AddComponent<CountingText>();
            _cyCount.Bind(_settings, "획득 $", true, "", true);
            _cyCount.Stepped += HandleCountStep;
            _cyCount.Completed += HandleCountComplete;

            _detail = UiFonts.CreateText(rect, "Detail", 16, TextAnchor.MiddleCenter);
            Stretch(_detail.rectTransform, new Vector2(0.08f, 0.68f), new Vector2(0.92f, 0.79f));
            _detail.fontSize = 16;
            _detail.supportRichText = true;
            _detail.color = Palette.Text;
            _detail.lineSpacing = 0.85f;

            _bestText = UiFonts.CreateText(rect, "BestText", 16, TextAnchor.MiddleCenter);
            Stretch(_bestText.rectTransform, new Vector2(0.08f, 0.555f), new Vector2(0.92f, 0.61f));
            _bestText.fontSize = 16;

            _goalText = UiFonts.CreateText(rect, "NextGoal", 16, TextAnchor.MiddleCenter);
            Stretch(_goalText.rectTransform, new Vector2(0.06f, 0.50f), new Vector2(0.94f, 0.555f));
            _goalText.fontSize = 16;
            _goalRect = _goalText.rectTransform;
            _goalScale = _goalRect.localScale;

            _zoneText = UiFonts.CreateText(rect, "NextZone", 16, TextAnchor.MiddleCenter);
            Stretch(_zoneText.rectTransform, new Vector2(0.06f, 0.455f), new Vector2(0.94f, 0.50f));
            _zoneText.fontSize = 16;
            _zoneText.color = Palette.Cyan;

            var loot = new GameObject("Loot", typeof(RectTransform));
            loot.transform.SetParent(rect, false);
            _lootRoot = (RectTransform)loot.transform;
            Stretch(_lootRoot, new Vector2(0.04f, 0.26f), new Vector2(0.96f, 0.45f));
            _lootGrid = loot.AddComponent<GridLayoutGroup>();
            _lootGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _lootGrid.constraintCount = 6;
            _lootGrid.cellSize = new Vector2(64f, 58f);
            _lootGrid.spacing = new Vector2(6f, 4f);
            _lootGrid.childAlignment = TextAnchor.UpperCenter;
            _lootGrid.startAxis = GridLayoutGroup.Axis.Horizontal;

            _restart = UiButton.Create(rect, "RestartButton", "재도전", new Vector2(0.25f, 0.15f), new Vector2(0.75f, 0.245f), () =>
            {
                if (_restartArmed)
                {
                    return;
                }

                _restartArmed = true;
                if (_restartRect != null)
                {
                    Fx.Punch(_restartRect, 0.18f, 0.15f);
                }

                var dur = FXConfig.Current.fadeDuration;
                Fx.FadeOut(dur, true);
                _restartAt = Time.unscaledTime + dur;
            });
            _restartRect = _restart.transform as RectTransform;
            _restartScale = _restartRect.localScale;

            if (onMarket != null)
            {
                var market = UiButton.Create(rect, "MarketButton", TitleView.MarketLabel, new Vector2(0.25f, 0.055f), new Vector2(0.75f, 0.13f), () =>
                {
                    if (!_restartArmed)
                    {
                        onMarket();
                    }
                });
                var outline = market.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.effectColor = Palette.Magenta;
                }

                var label = market.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.fontSize = 22;
                    label.color = Palette.Magenta;
                }
            }

            Hide();
        }

        void Update()
        {
            if (_restartArmed && Time.unscaledTime >= _restartAt)
            {
                _restartArmed = false;
                _onRestart?.Invoke();
            }

            if (_restart != null && _root.activeSelf)
            {
                _restart.interactable = Time.unscaledTime >= _readyAt;
            }

            if (_counting && _root != null && _root.activeSelf && Pressed())
            {
                if (_cyCount != null)
                {
                    _cyCount.Snap(_targetTotal);
                }

                PlayLoot();

                _counting = false;
                FinishCountAudio();
                if (_cyText != null)
                {
                    Fx.Punch(_cyText.transform, 0.16f, 0.16f);
                    Fx.FlashColor(_cyText, Color.white, 0.12f);
                }
            }
        }

        static bool Pressed()
        {
            return Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
        }

        public void Show(RunSession session)
        {
            _session = session;
            _bestText.text = $"최고 기록 {session.BestCY} CY";
            var goldHex = ColorUtility.ToHtmlStringRGB(Palette.Gold);
            _detail.text = $"CY {session.CY:N0}\n<color=#{goldHex}>골드 {session.Gold:N0}G → +${session.GoldDollars:N0}</color>\n─────────";
            var goal = Progression.NextGoal();
            _goalText.text = goal.Primary;
            _goalText.color = goal.CanBuy ? Palette.NeonGreen : goal.AllMaxed ? Palette.Gold : Palette.Text;
            _zoneText.text = goal.Zone ?? string.Empty;
            _newBest.SetActive(session.IsNewBest);
            _targetTotal = session.EarnedDollars;
            _counting = session.EarnedDollars > 0;
            _countAudioDone = false;
            _lootPlayed = false;
            _pendingNewBest = session.IsNewBest;
            _readyAt = Time.unscaledTime + InputLock;
            _restart.interactable = false;
            ClearLoot();
            _root.SetActive(true);
            PlayIntro(session);
            if (!_counting)
            {
                PlayLoot();
            }
        }

        public void Hide()
        {
            KillIntro();
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        void PlayIntro(RunSession session)
        {
            KillIntro();
            var fade = _settings != null ? _settings.resultsFadeDuration : 0.25f;
            var bestDur = _settings != null ? _settings.resultsBestDuration : 0.35f;
            var buttonDur = _settings != null ? _settings.resultsButtonDuration : 0.22f;

            _group.alpha = 0f;
            _group.blocksRaycasts = true;
            if (_dim != null)
            {
                var dim = _dim.color;
                dim.a = 0f;
                _dim.color = dim;
                _dim.DOFade(0.6f, fade).SetUpdate(true).SetLink(_root);
            }

            if (_cyCount != null)
            {
                _cyCount.Snap(0);
                if (session.EarnedDollars <= 0)
                {
                    _counting = false;
                    if (_pendingNewBest)
                    {
                        AudioManager.Ensure().PlayNewBest();
                    }
                }
                else
                {
                    _cyCount.PlayTo(session.EarnedDollars);
                }
            }
            else
            {
                _cyText.text = $"획득 ${session.EarnedDollars:N0}";
            }

            if (_restartRect != null)
            {
                _restartRect.localScale = Vector3.zero;
            }

            var stagger = FXConfig.Current.resultsStagger;
            Fx.Pop(_root.transform);
            if (_titleRect != null)
            {
                Fx.Pop(_titleRect);
            }

            _intro = DOTween.Sequence().SetUpdate(true).SetLink(_root);
            _intro.Append(_group.DOFade(1f, fade));
            if (_cyText != null)
            {
                _cyText.transform.localScale = Vector3.zero;
                _intro.Join(_cyText.transform.DOScale(1f, fade).SetDelay(stagger).SetEase(Ease.OutBack).SetUpdate(true));
            }

            if (_detail != null)
            {
                _detail.transform.localScale = Vector3.zero;
                _intro.Join(_detail.transform.DOScale(1f, fade).SetDelay(stagger * 2f).SetEase(Ease.OutBack).SetUpdate(true));
            }

            if (_bestText != null)
            {
                _bestText.transform.localScale = Vector3.zero;
                _intro.Join(_bestText.transform.DOScale(1f, fade).SetDelay(stagger * 3f).SetEase(Ease.OutBack).SetUpdate(true));
            }

            if (_goalRect != null)
            {
                _goalRect.localScale = _goalScale;
                var hint = Progression.NextGoal();
                if (hint.CanBuy)
                {
                    _goalRect.DOScale(_goalScale * 1.06f, 0.6f)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetEase(Ease.InOutSine)
                        .SetUpdate(true)
                        .SetLink(_goalText.gameObject);
                }
            }

            if (session.IsNewBest && _bestRect != null)
            {
                _bestRect.localScale = Vector3.zero;
                _intro.Join(_bestRect.DOScale(_bestScale, bestDur).SetEase(Ease.OutBack));
                _bestRect.DORotate(new Vector3(0f, 0f, 5f), 0.45f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(_newBest);
                var bestText = _newBest.GetComponent<Text>();
                if (bestText != null)
                {
                    bestText.DOColor(Color.white, 0.4f).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(_newBest);
                }

                Fx.Burst(_bestRect.position, null, 8, Palette.Gold);
            }

            _intro.AppendInterval(Mathf.Max(0f, InputLock - fade));
            if (_restartRect != null)
            {
                _intro.Append(_restartRect.DOScale(_restartScale, buttonDur).SetEase(Ease.OutBack));
                _intro.OnComplete(() =>
                {
                    if (_restartRect != null)
                    {
                        _restartRect.DOScale(_restartScale * 1.05f, 0.7f)
                            .SetLoops(-1, LoopType.Yoyo)
                            .SetEase(Ease.InOutSine)
                            .SetUpdate(true)
                            .SetLink(_restart.gameObject);
                    }
                });
            }
        }

        void HandleCountStep(int value)
        {
            if (_countAudioDone || _targetTotal <= 0)
            {
                return;
            }

            AudioManager.Ensure().PlayCountTick(Mathf.Clamp01(value / (float)_targetTotal));
        }

        void HandleCountComplete()
        {
            _counting = false;
            FinishCountAudio();
            PlayLoot();
            if (_cyText != null)
            {
                Fx.Punch(_cyText.transform, 0.16f, 0.16f);
                Fx.FlashColor(_cyText, Color.white, 0.12f);
            }
        }

        void FinishCountAudio()
        {
            if (_countAudioDone)
            {
                return;
            }

            _countAudioDone = true;
            AudioManager.Ensure().PlayCountDone();
            if (!_pendingNewBest)
            {
                return;
            }

            DOVirtual.DelayedCall(0.15f, () =>
            {
                AudioManager.Ensure().PlayNewBest();
            }).SetUpdate(true).SetLink(_root);
        }

        void KillIntro()
        {
            if (_intro != null && _intro.IsActive())
            {
                _intro.Kill(false);
            }

            _intro = null;
            if (_root != null)
            {
                _root.transform.DOKill(false);
            }

            if (_bestRect != null)
            {
                _bestRect.DOKill(false);
                _bestRect.localScale = _bestScale;
            }

            if (_restartRect != null)
            {
                _restartRect.DOKill(false);
            }

            if (_goalRect != null)
            {
                _goalRect.DOKill(false);
                _goalRect.localScale = _goalScale;
            }

            ClearLoot();
        }

        void PlayLoot()
        {
            if (_lootPlayed || _session == null || _lootRoot == null)
            {
                return;
            }

            _lootPlayed = true;
            ClearLoot();
            var stacks = _session.Loot;
            var hazards = _session.HazardCount > 0;
            var capacity = hazards ? 11 : 12;
            var overflow = stacks.Count > capacity;
            var shown = overflow ? capacity - 1 : stacks.Count;
            var index = 0;
            for (var i = 0; i < shown; i++)
            {
                var stack = stacks[i];
                SpawnCell(stack.Name, stack.Icon, stack.Placeholder, RarityGlow.BorderColor(stack.Rarity), stack.Count, stack.Rarity, index++);
            }

            if (overflow)
            {
                var extra = stacks.Count - shown;
                SpawnCell("+" + extra, null, Palette.Text, Palette.Text, 0, ItemRarity.Normal, index++, true);
            }

            if (hazards)
            {
                SpawnCell("위험물 x" + _session.HazardCount, null, RarityGlow.HazardBorder, RarityGlow.HazardBorder, 0, ItemRarity.Normal, index++, true);
            }
        }

        void SpawnCell(string label, Sprite icon, Color placeholder, Color border, int count, ItemRarity rarity, int index, bool caption = false)
        {
            var cell = new GameObject(caption ? "LootNote" : "LootCell");
            cell.transform.SetParent(_lootRoot, false);
            var background = cell.AddComponent<Image>();
            background.color = new Color(0.04f, 0.05f, 0.08f, 0.94f);
            background.raycastTarget = false;
            var outline = cell.AddComponent<Outline>();
            outline.effectColor = border;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;

            if (!caption)
            {
                var artGo = new GameObject("Icon");
                artGo.transform.SetParent(cell.transform, false);
                var art = artGo.AddComponent<Image>();
                art.sprite = icon != null ? icon : PlaceholderFactory.Square(placeholder, 16);
                art.preserveAspect = true;
                art.raycastTarget = false;
                var artRect = art.rectTransform;
                artRect.anchorMin = new Vector2(0.12f, 0.22f);
                artRect.anchorMax = new Vector2(0.88f, 0.92f);
                artRect.offsetMin = Vector2.zero;
                artRect.offsetMax = Vector2.zero;
                if (count > 1)
                {
                    var badge = UiFonts.CreateText(cell.transform, "Count", 11, TextAnchor.LowerRight);
                    badge.fontSize = 11;
                    badge.color = Color.white;
                    badge.text = "x" + count;
                    Stretch(badge.rectTransform, new Vector2(0.08f, 0.02f), new Vector2(0.96f, 0.38f));
                }
            }
            else
            {
                var captionText = UiFonts.CreateText(cell.transform, "Caption", 11, TextAnchor.MiddleCenter);
                captionText.fontSize = 11;
                captionText.color = border;
                captionText.text = label;
                Stretch(captionText.rectTransform, Vector2.zero, Vector2.one);
            }

            cell.transform.localScale = Vector3.zero;
            cell.transform.DOScale(1f, 0.22f)
                .SetDelay(index * 0.04f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .SetLink(cell);
            if (rarity == ItemRarity.Synchro)
            {
                // 테두리가 하양·핑크·연두로 번갈아 바뀌고, 배경도 같은 색으로 은은하게 물든다.
                var colors = RarityGlow.SynchroColors;
                var cycle = DOTween.Sequence().SetLoops(-1).SetUpdate(true).SetLink(cell);
                for (var i = 0; i < colors.Length; i++)
                {
                    var next = colors[(i + 1) % colors.Length];
                    cycle.Append(outline.DOColor(next, 0.45f).SetEase(Ease.InOutSine));
                    cycle.Join(background.DOColor(Color.Lerp(background.color, next, 0.4f), 0.45f).SetEase(Ease.InOutSine));
                }
            }
            else if (rarity == ItemRarity.Epic || rarity == ItemRarity.Legend)
            {
                background.DOColor(Color.Lerp(background.color, border, 0.45f), 0.7f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetUpdate(true)
                    .SetLink(cell);
            }
        }

        void ClearLoot()
        {
            if (_lootRoot == null)
            {
                return;
            }

            for (var i = _lootRoot.childCount - 1; i >= 0; i--)
            {
                var child = _lootRoot.GetChild(i);
                child.DOKill(false);
                Destroy(child.gameObject);
            }
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
