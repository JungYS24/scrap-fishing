using System;
using System.Collections.Generic;
using DG.Tweening;
using ScrapFishing.Audio;
using ScrapFishing.Core;
using ScrapFishing.FX;
using ScrapFishing.Scrap;
using ScrapFishing.UI;
using UnityEngine;

namespace ScrapFishing.Fishing
{
    public class LureCollector : MonoBehaviour
    {
        static readonly Vector2[] SlotPreset =
        {
            new Vector2(-0.35f, -0.5f),
            new Vector2(0.35f, -0.5f),
            new Vector2(0f, -0.95f),
            new Vector2(-0.55f, -1f),
            new Vector2(0.55f, -1f),
            new Vector2(0f, -1.45f)
        };

        [SerializeField] float attachAlpha = 0.8f;
        [SerializeField] float attachScale = 0.75f;
        [SerializeField] float followSmooth = 0.12f;
        [SerializeField] float swayAmount = 0.05f;
        [SerializeField] float swaySpeed = 2.4f;
        [SerializeField] float jumpPower = 1.5f;
        [SerializeField] float jumpDuration = 0.55f;
        [SerializeField] float stagger = 0.07f;
        [SerializeField] Transform settlePoint;

        readonly List<AttachedScrap> _attached = new List<AttachedScrap>();
        readonly LureLoad _load = new LureLoad(4);

        RunSession _session;
        HudView _hud;
        SpriteRenderer _lureRenderer;
        bool _settling;
        Sequence _settleSeq;
        Vector3 _lastLurePos;

        public event Action Changed;

        public int Count => _load.Count;
        public int MaxAttach => RodStats.MaxAttach;
        public bool HasAttached => _load.Count > 0;
        public bool IsSettling => _settling;

        void OnEnable()
        {
            Progression.Changed += HandleProgression;
        }

        void OnDisable()
        {
            Progression.Changed -= HandleProgression;
        }

        public void Bind(RunSession session, Transform point, HudView hud)
        {
            _session = session;
            settlePoint = point;
            _hud = hud;
            _lureRenderer = GetComponent<SpriteRenderer>();
            RefreshCapacity();
            Notify();
        }

        void HandleProgression()
        {
            RefreshCapacity();
            Notify();
        }

        public void SetSettlePoint(Transform point)
        {
            settlePoint = point;
        }

        public bool TryAttach(ScrapView scrap)
        {
            if (scrap == null || scrap.Attached || _settling)
            {
                return false;
            }

            RefreshCapacity();
            if (!_load.TryAttach(scrap.GetInstanceID(), scrap.ChipValue))
            {
                if (_load.IsFull)
                {
                    if (Fx.TryFullReject(scrap.transform.position, scrap.transform) && _hud != null)
                    {
                        _hud.NudgeLoad();
                    }

                    AudioManager.Ensure().PlayFull();
                }

                return false;
            }

            var drift = scrap.GetComponent<AmbientDrift>();
            if (drift != null)
            {
                drift.enabled = false;
            }

            var renderer = scrap.Renderer;
            var restColor = renderer != null ? renderer.color : Color.white;
            var restOrder = renderer != null ? renderer.sortingOrder : 2;
            var restScale = scrap.transform.localScale;
            var lureOrder = _lureRenderer != null ? _lureRenderer.sortingOrder : 6;
            var slot = _attached.Count;
            scrap.Attached = true;
            scrap.ApplyHang(attachAlpha, restScale * attachScale, lureOrder - 1 - slot);
            Fx.Punch(scrap.transform, 0.3f + slot * 0.02f, 0.18f);
            if (renderer != null)
            {
                Fx.FlashColor(renderer, Color.white, 0.1f);
            }

            Fx.Punch(transform, 0.1f + slot * 0.015f, 0.16f);
            AudioManager.Ensure().PlayCatch(scrap);

            _attached.Add(new AttachedScrap
            {
                View = scrap,
                Velocity = Vector3.zero,
                RestScale = restScale,
                RestColor = restColor,
                RestOrder = restOrder,
                Slot = slot
            });
            Notify();
            return true;
        }

        public void SettleAll(Action onDone)
        {
            if (_settling)
            {
                return;
            }

            if (_attached.Count == 0)
            {
                onDone?.Invoke();
                return;
            }

            _settling = true;
            KillSettle();
            var target = SettleWorld();
            var remaining = _attached.Count;
            _settleSeq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            for (var i = 0; i < _attached.Count; i++)
            {
                var item = _attached[i];
                var delay = i * stagger;
                _settleSeq.InsertCallback(delay, () => Launch(item, target, () =>
                {
                    remaining--;
                    if (remaining <= 0)
                    {
                        FinishSettle(onDone);
                    }
                }));
            }
        }

        public void SettleImmediate()
        {
            KillSettle();
            _settling = true;
            for (var i = 0; i < _attached.Count; i++)
            {
                Cash(_attached[i], false);
            }

            FinishSettle(null);
        }

        public void DiscardAttached()
        {
            KillSettle();
            for (var i = 0; i < _attached.Count; i++)
            {
                var view = _attached[i].View;
                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }

            _attached.Clear();
            _load.Clear();
            _settling = false;
            Notify();
        }

        void Update()
        {
            if (_settling)
            {
                return;
            }

            var dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            var lureSpeed = ((Vector2)(transform.position - _lastLurePos)).magnitude / dt;
            _lastLurePos = transform.position;
            var boost = FXConfig.Enabled
                ? 1f + Mathf.Clamp(lureSpeed * 0.08f, 0f, 1.1f) * FXConfig.Current.swayBoost
                : 1f;
            var t = Time.unscaledTime * swaySpeed;
            for (var i = 0; i < _attached.Count; i++)
            {
                var item = _attached[i];
                if (item.View == null)
                {
                    continue;
                }

                var goal = SlotWorld(item.Slot, t, boost);
                var pos = Vector3.SmoothDamp(item.View.transform.position, goal, ref item.Velocity, followSmooth, Mathf.Infinity, dt);
                item.View.transform.position = pos;
                var tilt = Mathf.Sin(t + item.Slot) * 8f;
                item.View.transform.rotation = Quaternion.Euler(0f, 0f, tilt);
                PulseRare(item, t);
            }
        }

        static void PulseRare(AttachedScrap item, float time)
        {
            if (!FXConfig.Enabled || item.View == null)
            {
                return;
            }

            var grade = item.View.Definition != null ? item.View.Definition.Grade : ScrapGrade.Junk;
            if (grade < ScrapGrade.Rare)
            {
                return;
            }

            var renderer = item.View.Renderer;
            if (renderer == null)
            {
                return;
            }

            var cfg = FXConfig.Current;
            var wave = 0.5f + 0.5f * Mathf.Sin(time * 1.6f + item.Slot);
            var color = renderer.color;
            color.a = Mathf.Lerp(cfg.rarePulseMin, cfg.rarePulseMax, wave);
            renderer.color = color;
        }

        void Launch(AttachedScrap item, Vector3 target, Action arrived)
        {
            if (item.View == null)
            {
                arrived?.Invoke();
                return;
            }

            item.View.RestoreLook(item.RestColor, item.RestScale, item.RestOrder);
            var body = item.View.transform;
            body.DOKill(false);
            var jump = body.DOJump(target, jumpPower, 1, jumpDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(item.View.gameObject);
            jump.OnComplete(() =>
            {
                if (item.View == null)
                {
                    arrived?.Invoke();
                    return;
                }

                if (FXConfig.Enabled)
                {
                    body.DOPunchScale(Vector3.one * 0.18f, 0.14f, 6, 0.6f)
                        .SetUpdate(true)
                        .SetLink(item.View.gameObject)
                        .OnComplete(() =>
                        {
                            Cash(item, true);
                            arrived?.Invoke();
                        });
                    return;
                }

                Cash(item, true);
                arrived?.Invoke();
            });
        }

        void Cash(AttachedScrap item, bool popup)
        {
            if (item.View == null)
            {
                return;
            }

            if (!_load.TryCash(item.View.GetInstanceID()))
            {
                return;
            }

            var pos = item.View.transform.position;
            var chip = 0;
            var gold = 0;
            if (_session != null)
            {
                _session.AddSettledCatch(item.View.Definition, item.View.data, pos, out chip, out gold);
            }

            if (popup)
            {
                Fx.FloatText(pos + Vector3.up * 0.12f, "+" + chip, Palette.Cyan);
                Fx.FloatText(pos, "+" + gold + "G", Palette.Gold);
                var grade = item.View.Definition != null ? item.View.Definition.Grade : ScrapGrade.Common;
                var bits = grade >= ScrapGrade.Rare ? 10 : 5;
                Fx.Burst(pos, null, bits, Palette.Gold);
                if (grade >= ScrapGrade.Rare)
                {
                    Fx.Ring(pos, Palette.Cyan, 0.32f);
                }

                if (grade >= ScrapGrade.Mutant)
                {
                    Fx.ScreenFlash(Color.white, 0.08f);
                }
            }

            Destroy(item.View.gameObject);
            item.View = null;
        }

        void FinishSettle(Action onDone)
        {
            var had = _attached.Count > 0;
            KillSettle();
            _attached.Clear();
            _load.Clear();
            _settling = false;
            Notify();
            if (had)
            {
                var boat = GameObject.Find("Nomad");
                if (boat != null)
                {
                    Fx.Punch(boat.transform, 0.22f, 0.22f);
                }

                Fx.ShakeCamera(FXConfig.Current.finaleShake, FXConfig.Current.finaleShakeDur);
            }

            onDone?.Invoke();
        }

        void KillSettle()
        {
            if (_settleSeq != null && _settleSeq.IsActive())
            {
                _settleSeq.Kill(false);
            }

            _settleSeq = null;
            for (var i = 0; i < _attached.Count; i++)
            {
                if (_attached[i].View != null)
                {
                    _attached[i].View.transform.DOKill(false);
                }
            }
        }

        void RefreshCapacity()
        {
            _load.SetMaxAttach(RodStats.MaxAttach);
        }

        void Notify()
        {
            Changed?.Invoke();
            if (_hud != null)
            {
                _hud.SetLoad(_load.Count, RodStats.MaxAttach);
            }
        }

        Vector3 SlotWorld(int slot, float time, float boost = 1f)
        {
            var offset = OffsetFor(slot);
            offset.x += Mathf.Sin(time + slot) * swayAmount * boost;
            offset.y += Mathf.Cos(time * 0.7f + slot) * swayAmount * 0.35f * boost;
            return transform.position + transform.rotation * (Vector3)offset;
        }

        static Vector2 OffsetFor(int index)
        {
            if (index < SlotPreset.Length)
            {
                return SlotPreset[index];
            }

            var extra = index - SlotPreset.Length;
            var row = 3 + extra / 2;
            var side = extra % 2 == 0 ? -1f : 1f;
            return new Vector2(side * (0.4f + row * 0.06f), -0.5f - row * 0.48f);
        }

        Vector3 SettleWorld()
        {
            return settlePoint != null ? settlePoint.position : new Vector3(0f, 0.85f, 0f);
        }

        class AttachedScrap
        {
            public ScrapView View;
            public Vector3 Velocity;
            public Vector3 RestScale;
            public Color RestColor;
            public int RestOrder;
            public int Slot;
        }
    }
}
