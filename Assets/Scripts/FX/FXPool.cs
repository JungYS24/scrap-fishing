using System.Collections.Generic;
using DG.Tweening;
using ScrapFishing.Scrap;
using ScrapFishing.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.FX
{
    public static class FXPool
    {
        static readonly Queue<SpriteRenderer> Bits = new Queue<SpriteRenderer>(32);
        static readonly Queue<Text> Labels = new Queue<Text>(8);
        static readonly List<SpriteRenderer> LiveBits = new List<SpriteRenderer>(32);

        static Transform _bitRoot;
        static RectTransform _labelRoot;
        static Sprite _circle;
        static Sprite _square;

        public static Sprite Circle
        {
            get
            {
                if (_circle == null)
                {
                    _circle = PlaceholderFactory.Circle(Color.white, 16);
                }

                return _circle;
            }
        }

        public static Sprite Square
        {
            get
            {
                if (_square == null)
                {
                    _square = PlaceholderFactory.Square(Color.white, 8);
                }

                return _square;
            }
        }

        public static int LiveBitCount => LiveBits.Count;
        public static int MaxBits => FXConfig.Current.maxBurstBits;

        public static void Bind(Transform bitRoot, RectTransform labelRoot)
        {
            _bitRoot = bitRoot;
            _labelRoot = labelRoot;
        }

        public static SpriteRenderer TakeBit()
        {
            if (LiveBits.Count >= MaxBits)
            {
                return null;
            }

            SpriteRenderer bit;
            if (Bits.Count > 0)
            {
                bit = Bits.Dequeue();
            }
            else
            {
                var go = new GameObject("FXBit");
                if (_bitRoot != null)
                {
                    go.transform.SetParent(_bitRoot, false);
                }

                bit = go.AddComponent<SpriteRenderer>();
                bit.sprite = Circle;
                bit.sortingOrder = 20;
            }

            bit.gameObject.SetActive(true);
            LiveBits.Add(bit);
            return bit;
        }

        public static void ReturnBit(SpriteRenderer bit)
        {
            if (bit == null)
            {
                return;
            }

            LiveBits.Remove(bit);
            bit.transform.DOKill(false);
            bit.DOKill(false);
            bit.gameObject.SetActive(false);
            Bits.Enqueue(bit);
        }

        public static Text TakeLabel()
        {
            if (_labelRoot == null)
            {
                return null;
            }

            Text label;
            if (Labels.Count > 0)
            {
                label = Labels.Dequeue();
            }
            else
            {
                label = UiFonts.CreateText(_labelRoot, "FXFloat", 22, TextAnchor.MiddleCenter);
                var rect = label.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(240f, 48f);
                if (label.GetComponent<CanvasGroup>() == null)
                {
                    label.gameObject.AddComponent<CanvasGroup>();
                }
            }

            label.gameObject.SetActive(true);
            return label;
        }

        public static void ReturnLabel(Text label)
        {
            if (label == null)
            {
                return;
            }

            label.transform.DOKill(false);
            label.DOKill(false);
            var group = label.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.DOKill(false);
            }

            label.gameObject.SetActive(false);
            Labels.Enqueue(label);
        }

        public static void RecycleAll()
        {
            for (var i = LiveBits.Count - 1; i >= 0; i--)
            {
                ReturnBit(LiveBits[i]);
            }
        }
    }
}
