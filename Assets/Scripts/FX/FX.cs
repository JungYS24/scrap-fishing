using DG.Tweening;
using ScrapFishing.Core;
using ScrapFishing.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ScrapFishing.FX
{
    public static class Fx
    {
        static GameObject _host;
        static Canvas _canvas;
        static RectTransform _layer;
        static Image _fade;
        static Image _vignette;
        static CanvasGroup _fadeGroup;
        static CanvasGroup _vignetteGroup;
        static FXCameraShake _shake;
        static float _fullAt;
        static Tween _vignettePulse;
        static bool _booted;
        static float _lockUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (_booted)
            {
                return;
            }

            _booted = true;
            var cfg = FXConfig.Current;
            var ui = Resources.Load<UiFxSettings>("UiFxSettings");
            var tweens = Mathf.Max(cfg.tweenCapacity, ui != null ? ui.tweenCapacity : 0);
            var seq = Mathf.Max(cfg.sequenceCapacity, ui != null ? ui.sequenceCapacity : 0);
            DOTween.Init(true, true, LogBehaviour.ErrorsOnly);
            DOTween.SetTweensCapacity(Mathf.Max(200, tweens), Mathf.Max(50, seq));
            DOTween.defaultAutoKill = true;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _lockUntil = 0f;
            if (_canvas == null)
            {
                _fade = null;
                _fadeGroup = null;
                _layer = null;
                _shake = null;
            }
        }

        public static void Ensure()
        {
            Boot();
            if (_host == null)
            {
                _host = new GameObject("FXRuntime");
                if (Application.isPlaying)
                {
                    Object.DontDestroyOnLoad(_host);
                }
                var bits = new GameObject("FXBits");
                bits.transform.SetParent(_host.transform, false);
            }

            if (!_host.TryGetComponent<FXRuntimeHost>(out _))
            {
                _host.AddComponent<FXRuntimeHost>();
            }

            if (_canvas == null || _fade == null)
            {
                BindCanvas();
            }

            if (_shake == null && Camera.main != null)
            {
                _shake = GetOrAdd<FXCameraShake>(Camera.main.gameObject);
            }
        }

        static void BindCanvas()
        {
            _canvas = Object.FindFirstObjectByType<Canvas>();
            if (_canvas == null)
            {
                return;
            }

            var existing = _canvas.transform.Find("FXLayer") as RectTransform;
            if (existing != null)
            {
                _layer = existing;
            }
            else
            {
                var go = new GameObject("FXLayer", typeof(RectTransform));
                go.transform.SetParent(_canvas.transform, false);
                _layer = (RectTransform)go.transform;
                Stretch(_layer);
                go.transform.SetAsLastSibling();
            }

            _fade = ChildImage(_layer, "FXFade", new Color(0f, 0f, 0f, 0f));
            _fadeGroup = GetOrAdd<CanvasGroup>(_fade.gameObject);
            ResetFadeVisual();
            _vignette = ChildImage(_layer, "FXVignette", new Color(0.7f, 0.05f, 0.12f, 0f));
            _vignette.raycastTarget = false;
            _vignetteGroup = GetOrAdd<CanvasGroup>(_vignette.gameObject);
            _vignetteGroup.alpha = 0f;
            FXPool.Bind(_host != null ? _host.transform.Find("FXBits") : null, _layer);
        }

        static Image ChildImage(RectTransform parent, string name, Color color)
        {
            var child = parent.Find(name);
            Image image;
            if (child != null)
            {
                image = child.GetComponent<Image>();
                if (image != null)
                {
                    return image;
                }
            }

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            image = go.AddComponent<Image>();
            image.sprite = FXPool.Square;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void Punch(Transform t, float scale = 0.2f, float dur = 0.2f)
        {
            if (!FXConfig.Enabled || t == null)
            {
                return;
            }

            Ensure();
            var rest = FxRest.Capture(t);
            t.DOKill(false);
            rest.RestoreScale();
            t.DOPunchScale(Vector3.one * scale, dur, 8, 0.6f)
                .SetUpdate(true)
                .SetLink(t.gameObject)
                .OnComplete(rest.RestoreScale)
                .OnKill(rest.RestoreScale);
        }

        public static void Pop(Transform t)
        {
            if (!FXConfig.Enabled || t == null)
            {
                return;
            }

            Ensure();
            var cfg = FXConfig.Current;
            var rest = FxRest.Capture(t);
            t.DOKill(false);
            rest.RestoreScale();
            var baseScale = rest.Scale;
            t.localScale = Vector3.zero;
            var seq = DOTween.Sequence().SetUpdate(true).SetLink(t.gameObject);
            seq.Append(t.DOScale(baseScale * cfg.popOvershoot, cfg.popDuration * 0.65f).SetEase(Ease.OutBack));
            seq.Append(t.DOScale(baseScale, cfg.popDuration * 0.35f));
            seq.OnComplete(rest.RestoreScale);
            seq.OnKill(rest.RestoreScale);
        }

        public static void Shake(Transform t, float strength, float dur)
        {
            if (!FXConfig.Enabled || t == null)
            {
                return;
            }

            Ensure();
            var rest = FxRest.Capture(t);
            t.DOKill(false);
            rest.Restore();
            var rect = t as RectTransform;
            Tween tween = rect != null
                ? rect.DOShakeAnchorPos(dur, strength, 12, 90f, false, true)
                : t.DOShakePosition(dur, strength, 12, 90f, false, true);
            tween.SetUpdate(true).SetLink(t.gameObject).OnComplete(rest.Restore).OnKill(rest.Restore);
        }

        public static void Restore(Transform t)
        {
            if (t == null)
            {
                return;
            }

            var rest = t.GetComponent<FxRest>();
            if (rest != null)
            {
                rest.Restore();
            }
        }

        public static void ShakeCamera(float strength, float dur)
        {
            if (!FXConfig.Enabled)
            {
                return;
            }

            Ensure();
            if (_shake == null && Camera.main != null)
            {
                _shake = GetOrAdd<FXCameraShake>(Camera.main.gameObject);
            }

            if (_shake != null)
            {
                _shake.Play(strength, dur);
            }
        }

        public static void FlashColor(SpriteRenderer g, Color c, float dur)
        {
            if (!FXConfig.Enabled || g == null)
            {
                return;
            }

            Ensure();
            var rest = g.color;
            g.DOKill(false);
            g.color = c;
            g.DOColor(rest, dur).SetUpdate(true).SetLink(g.gameObject);
        }

        public static void FlashColor(Graphic g, Color c, float dur)
        {
            if (!FXConfig.Enabled || g == null)
            {
                return;
            }

            Ensure();
            var rest = g.color;
            g.DOKill(false);
            g.color = c;
            g.DOColor(rest, dur).SetUpdate(true).SetLink(g.gameObject);
        }

        public static void FloatText(Vector3 worldPos, string text, Color c)
        {
            if (!FXConfig.Enabled)
            {
                return;
            }

            Ensure();
            if (_layer == null)
            {
                BindCanvas();
            }

            var label = FXPool.TakeLabel();
            if (label == null)
            {
                return;
            }

            var cfg = FXConfig.Current;
            label.text = text;
            label.color = c;
            var group = label.GetComponent<CanvasGroup>();
            if (group == null)
            {
                FXPool.ReturnLabel(label);
                return;
            }

            group.alpha = 1f;
            var rect = label.rectTransform;
            if (WorldToLocal(worldPos, out var local))
            {
                rect.anchoredPosition = local;
            }

            rect.DOKill(false);
            group.DOKill(false);
            var seq = DOTween.Sequence().SetUpdate(true).SetLink(label.gameObject);
            seq.Append(rect.DOAnchorPos(rect.anchoredPosition + Vector2.up * (cfg.floatRise * 80f), cfg.floatDuration).SetEase(Ease.OutQuad));
            seq.Join(group.DOFade(0f, cfg.floatDuration));
            seq.OnComplete(() => FXPool.ReturnLabel(label));
        }

        public static void Burst(Vector3 pos, Sprite spr, int count, Color c)
        {
            if (!FXConfig.Enabled)
            {
                return;
            }

            Ensure();
            var room = FXPool.MaxBits - FXPool.LiveBitCount;
            count = Mathf.Min(count, room);
            if (count <= 0)
            {
                return;
            }

            var sprite = spr != null ? spr : FXPool.Circle;
            for (var i = 0; i < count; i++)
            {
                var bit = FXPool.TakeBit();
                if (bit == null)
                {
                    return;
                }

                bit.sprite = sprite;
                bit.color = c;
                bit.transform.position = pos;
                bit.transform.localScale = Vector3.one * Random.Range(0.05f, 0.1f);
                var dir = Random.insideUnitCircle.normalized;
                if (dir.sqrMagnitude < 0.01f)
                {
                    dir = Vector2.up;
                }

                var dest = pos + (Vector3)(dir * Random.Range(0.25f, 0.7f));
                var dur = Random.Range(0.28f, 0.45f);
                bit.transform.DOKill(false);
                bit.DOKill(false);
                var seq = DOTween.Sequence().SetUpdate(true).SetLink(bit.gameObject);
                seq.Append(bit.transform.DOMove(dest, dur).SetEase(Ease.OutQuad));
                seq.Join(bit.DOFade(0f, dur));
                seq.OnComplete(() => FXPool.ReturnBit(bit));
            }
        }

        public static void Splash(Vector3 pos, float strength)
        {
            if (!FXConfig.Enabled)
            {
                return;
            }

            Ensure();
            var drops = Mathf.RoundToInt(FXConfig.Current.splashDrops * strength);
            Burst(pos, FXPool.Circle, drops, Color.Lerp(Palette.Cyan, Color.white, 0.4f));
            Ring(pos, Color.Lerp(Palette.Cyan, Color.white, 0.35f), 0.38f * strength);
        }

        public static void Ring(Vector3 pos, Color c, float dur)
        {
            if (!FXConfig.Enabled)
            {
                return;
            }

            Ensure();
            var bit = FXPool.TakeBit();
            if (bit == null)
            {
                return;
            }

            bit.sprite = FXPool.Circle;
            bit.color = c;
            bit.transform.position = pos;
            bit.transform.localScale = new Vector3(0.18f, 0.06f, 1f);
            bit.transform.DOKill(false);
            bit.DOKill(false);
            var seq = DOTween.Sequence().SetUpdate(true).SetLink(bit.gameObject);
            seq.Append(bit.transform.DOScale(new Vector3(0.95f, 0.14f, 1f), dur).SetEase(Ease.OutQuad));
            seq.Join(bit.DOFade(0f, dur));
            seq.OnComplete(() => FXPool.ReturnBit(bit));
        }

        public static void ScreenFlash(Color c, float dur, float alpha = 0.55f)
        {
            if (!FXConfig.Enabled)
            {
                return;
            }

            Ensure();
            if (_fade == null)
            {
                BindCanvas();
            }

            if (_fade == null)
            {
                return;
            }

            _fade.color = c;
            _fadeGroup.DOKill(false);
            UnlockFade();
            _fadeGroup.alpha = alpha;
            _fadeGroup.DOFade(0f, dur).SetUpdate(true).SetLink(_fade.gameObject);
        }

        public static void FadeOut(float dur, bool lockInput)
        {
            if (!FXConfig.Enabled)
            {
                return;
            }

            Ensure();
            if (_fadeGroup == null)
            {
                BindCanvas();
            }

            if (_fadeGroup == null)
            {
                return;
            }

            _fade.color = Color.black;
            _fade.raycastTarget = lockInput;
            _fadeGroup.DOKill(false);
            _fadeGroup.blocksRaycasts = lockInput;
            _lockUntil = lockInput ? Time.unscaledTime + 0.6f : 0f;
            _fadeGroup.DOFade(1f, dur).SetUpdate(true).SetLink(_fade.gameObject);
        }

        public static void FadeIn(float dur)
        {
            if (!FXConfig.Enabled)
            {
                UnlockFade();
                return;
            }

            Ensure();
            if (_fadeGroup == null)
            {
                BindCanvas();
            }

            if (_fadeGroup == null)
            {
                return;
            }

            _fade.color = Color.black;
            _fadeGroup.DOKill(false);
            _lockUntil = 0f;
            _fadeGroup.DOFade(0f, dur).SetUpdate(true).SetLink(_fade.gameObject)
                .OnComplete(() =>
                {
                    ResetFadeVisual();
                });
        }

        public static void PulseFade(float dur)
        {
            if (!FXConfig.Enabled)
            {
                return;
            }

            Ensure();
            var outDur = Mathf.Clamp(dur * 0.45f, 0.08f, 0.18f);
            var inDur = Mathf.Clamp(dur * 0.55f, 0.1f, 0.22f);
            FadeOut(outDur, true);
            var host = _host != null ? _host : _fade != null ? _fade.gameObject : null;
            var call = DOVirtual.DelayedCall(outDur, () => FadeIn(inDur)).SetUpdate(true);
            if (host != null)
            {
                call.SetLink(host);
            }
        }

        static void UnlockFade()
        {
            _lockUntil = 0f;
            if (_fadeGroup != null)
            {
                _fadeGroup.blocksRaycasts = false;
            }

            if (_fade != null)
            {
                _fade.raycastTarget = false;
            }
        }

        static void ResetFadeVisual()
        {
            UnlockFade();
            if (_fadeGroup != null)
            {
                _fadeGroup.DOKill(false);
                _fadeGroup.alpha = 0f;
            }

            if (_fade != null)
            {
                _fade.color = new Color(0f, 0f, 0f, 0f);
            }
        }

        internal static void TickLockWatchdog()
        {
            if (_lockUntil <= 0f || Time.unscaledTime < _lockUntil)
            {
                return;
            }

            if (_fadeGroup != null && _fadeGroup.alpha > 0.05f)
            {
                FadeIn(0.12f);
                return;
            }

            ResetFadeVisual();
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            if (go.TryGetComponent<T>(out var existing))
            {
                return existing;
            }

            return go.AddComponent<T>();
        }

        public static void SetVignette(float alpha, float pulse)
        {
            Ensure();
            if (_vignetteGroup == null)
            {
                BindCanvas();
            }

            if (_vignetteGroup == null)
            {
                return;
            }

            if (!FXConfig.Enabled)
            {
                ClearVignette();
                return;
            }

            if (_vignettePulse != null && _vignettePulse.IsActive())
            {
                _vignettePulse.Kill(false);
            }

            _vignetteGroup.alpha = alpha;
            _vignettePulse = _vignetteGroup.DOFade(alpha * 0.45f, pulse)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(_vignette.gameObject);
        }

        public static void ClearVignette()
        {
            if (_vignettePulse != null && _vignettePulse.IsActive())
            {
                _vignettePulse.Kill(false);
            }

            _vignettePulse = null;
            if (_vignetteGroup != null)
            {
                _vignetteGroup.DOKill(false);
                _vignetteGroup.alpha = 0f;
            }
        }

        public static bool TryFullReject(Vector3 worldPos, Transform scrap)
        {
            if (!FXConfig.Enabled)
            {
                return false;
            }

            if (Time.unscaledTime < _fullAt)
            {
                return false;
            }

            _fullAt = Time.unscaledTime + FXConfig.Current.fullCooldown;
            FloatText(worldPos, "FULL", Palette.Magenta);
            if (scrap != null)
            {
                scrap.DOKill(false);
                scrap.DOPunchPosition(Vector3.right * FXConfig.Current.fullNudge, 0.18f, 6, 0.4f)
                    .SetUpdate(true)
                    .SetLink(scrap.gameObject);
            }

            return true;
        }

        public static void SuckIn(Vector3 from, Vector3 to, Sprite spr, Vector3 scale, int amount)
        {
            if (!FXConfig.Enabled)
            {
                return;
            }

            Ensure();
            var bit = FXPool.TakeBit();
            if (bit == null)
            {
                Burst(to, null, 4, Palette.Cyan);
                FloatText(to, amount >= 0 ? "+" + amount : amount.ToString(), amount >= 0 ? Palette.Cyan : Palette.Magenta);
                return;
            }

            bit.sprite = spr != null ? spr : FXPool.Square;
            bit.color = Color.white;
            bit.transform.position = from;
            bit.transform.localScale = scale;
            bit.transform.DOKill(false);
            bit.DOKill(false);
            var dur = 0.18f;
            var seq = DOTween.Sequence().SetUpdate(true).SetLink(bit.gameObject);
            seq.Append(bit.transform.DOMove(to, dur).SetEase(Ease.InQuad));
            seq.Join(bit.transform.DOScale(scale * 0.35f, dur));
            seq.OnComplete(() =>
            {
                var hit = bit.transform.position;
                FXPool.ReturnBit(bit);
                Burst(hit, null, 5, Palette.Cyan);
                FloatText(hit, amount >= 0 ? "+" + amount : amount.ToString(), amount >= 0 ? Palette.Cyan : Palette.Magenta);
            });
        }

        public static void Recycle()
        {
            FXPool.RecycleAll();
            ClearVignette();
            ResetFadeVisual();
        }

        static bool WorldToLocal(Vector3 world, out Vector2 local)
        {
            local = Vector2.zero;
            if (_canvas == null)
            {
                return false;
            }

            var cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _layer != null ? _layer : _canvas.transform as RectTransform,
                RectTransformUtility.WorldToScreenPoint(Camera.main, world),
                cam,
                out local);
        }
    }

    sealed class FxRest : MonoBehaviour
    {
        public Vector3 Scale { get; private set; }
        bool _captured;
        bool _rect;
        Vector2 _anchored;
        Vector3 _position;

        public static FxRest Capture(Transform target)
        {
            var rest = target.GetComponent<FxRest>();
            if (rest == null)
            {
                rest = target.gameObject.AddComponent<FxRest>();
                rest.hideFlags = HideFlags.HideInInspector;
            }

            rest.CaptureOnce();
            return rest;
        }

        public void RestoreScale()
        {
            if (!_captured || this == null)
            {
                return;
            }

            transform.localScale = Scale;
        }

        public void Restore()
        {
            RestoreScale();
            if (!_captured || this == null)
            {
                return;
            }

            if (_rect)
            {
                ((RectTransform)transform).anchoredPosition = _anchored;
            }
            else
            {
                transform.localPosition = _position;
            }
        }

        void CaptureOnce()
        {
            if (_captured)
            {
                return;
            }

            Scale = transform.localScale;
            if (transform is RectTransform rect)
            {
                _rect = true;
                _anchored = rect.anchoredPosition;
            }
            else
            {
                _position = transform.localPosition;
            }

            _captured = true;
        }
    }

    sealed class FXRuntimeHost : MonoBehaviour
    {
        void Update()
        {
            Fx.TickLockWatchdog();
        }
    }
}
