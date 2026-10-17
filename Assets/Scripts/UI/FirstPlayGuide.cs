using ScrapFishing.Boat;
using ScrapFishing.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    public class FirstPlayGuide : MonoBehaviour
    {
        const string Key = "TutorialDone";
        const int GaugeStep = 1;
        const int Done = 2;

        GameFlow _flow;
        CastingController _casting;
        RectTransform _root;
        Image _finger;
        Text _label;
        int _showing;

        public void Bind(RectTransform parent, GameFlow flow, CastingController casting)
        {
            _flow = flow;
            _casting = casting;
            if (PlayerPrefs.GetInt(Key, 0) >= Done || parent == null)
            {
                return;
            }

            Build(parent);
        }

        void Update()
        {
            if (_root == null || PlayerPrefs.GetInt(Key, 0) >= Done)
            {
                return;
            }

            if (_showing > 0 && Pressed())
            {
                Dismiss();
            }

            var step = PlayerPrefs.GetInt(Key, 0);
            if (step < GaugeStep && _flow != null && _flow.Phase == GamePhase.Aiming)
            {
                Show(GaugeStep, "탭해서 수심 결정", new Vector2(-36f, 40f), new Vector2(0.72f, 0.18f));
            }
            else if (step == GaugeStep && _casting != null && _casting.IsDescending)
            {
                Show(Done, "좌우로 밀어서 조종", new Vector2(0f, -8f), new Vector2(0.5f, 0.46f));
            }
            else if (_showing == GaugeStep && (_flow == null || _flow.Phase != GamePhase.Aiming))
            {
                Dismiss();
            }
            else if (_showing == Done && (_casting == null || !_casting.IsDescending))
            {
                Dismiss();
            }

            if (_finger != null && _root.gameObject.activeSelf)
            {
                var bob = Mathf.Sin(Time.unscaledTime * 5f) * 8f;
                _finger.rectTransform.anchoredPosition = new Vector2(_finger.rectTransform.anchoredPosition.x, bob);
            }
        }

        void Show(int step, string message, Vector2 finger, Vector2 anchor)
        {
            if (_showing == step)
            {
                return;
            }

            _showing = step;
            _root.gameObject.SetActive(true);
            _root.anchorMin = anchor;
            _root.anchorMax = anchor;
            _root.anchoredPosition = Vector2.zero;
            _label.text = message;
            _finger.rectTransform.anchoredPosition = finger;
        }

        void Dismiss()
        {
            if (_showing <= 0)
            {
                return;
            }

            var step = _showing;
            _showing = 0;
            _root.gameObject.SetActive(false);
            if (PlayerPrefs.GetInt(Key, 0) < step)
            {
                PlayerPrefs.SetInt(Key, step);
                PlayerPrefs.Save();
            }
        }

        static bool Pressed()
        {
            return Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
        }

        void Build(RectTransform parent)
        {
            var go = new GameObject("FirstPlayGuide");
            go.transform.SetParent(parent, false);
            _root = go.AddComponent<RectTransform>();
            _root.sizeDelta = new Vector2(280f, 120f);
            _root.pivot = new Vector2(0.5f, 0.5f);

            _label = UiFonts.CreateText(_root, "Label", 22, TextAnchor.MiddleCenter);
            var labelRect = _label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0.55f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            _label.color = Color.white;

            var fingerGo = new GameObject("Finger");
            fingerGo.transform.SetParent(_root, false);
            var fingerRect = fingerGo.AddComponent<RectTransform>();
            fingerRect.sizeDelta = new Vector2(36f, 48f);
            _finger = fingerGo.AddComponent<Image>();
            _finger.sprite = FingerSprite();
            _finger.color = Color.white;
            _finger.raycastTarget = false;
            _root.gameObject.SetActive(false);
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
                        ? new Color32(255, 255, 255, 230)
                        : new Color32(0, 0, 0, 0);
                }
            }

            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.2f), 32f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
