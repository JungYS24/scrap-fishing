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
        static readonly List<Text> LiveLabels = new List<Text>(16);
        // 조각·글자마다 지금 돌고 있는 시퀀스. DOKill(대상)은 시퀀스 안에 든 트윈을 멈추지 못하므로 시퀀스를 직접 끈다.
        // 끄지 않으면 돌려놓은 뒤에도 옛 시퀀스가 계속 움직이고, 끝날 때 한 번 더 돌려놓아 풀에 같은 오브젝트가 두 번 들어간다.
        static readonly Dictionary<Component, Tween> Running = new Dictionary<Component, Tween>(48);

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
            if (_labelRoot != labelRoot)
            {
                Labels.Clear();
                _labelRoot = labelRoot;
            }

            _bitRoot = bitRoot;
            DropDestroyedBits();
        }

        public static SpriteRenderer TakeBit()
        {
            if (LiveBits.Count >= MaxBits)
            {
                return null;
            }

            SpriteRenderer bit = null;
            while (Bits.Count > 0 && bit == null)
            {
                bit = Bits.Dequeue();
            }

            if (bit == null)
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

        // 꺼낸 조각·글자를 움직이는 시퀀스를 등록한다. 돌려놓을 때 이 시퀀스를 끈다.
        public static void Track(Component item, Tween sequence)
        {
            if (item == null)
            {
                return;
            }

            StopRunning(item);
            // DOTween.Init(재활용 켬) 때문에 끝난 시퀀스 객체는 다른 연출에 다시 쓰인다. 여기 들고 있는 시퀀스가 재활용되면
            // 나중에 끌 때 엉뚱한 글자의 시퀀스를 끄게 되어 그 글자(FULL, -23 CY 등)가 판이 끝날 때까지 화면에 남는다.
            sequence.SetRecyclable(false);
            Running[item] = sequence;
        }

        static void StopRunning(Component item)
        {
            if (Running.TryGetValue(item, out var running))
            {
                Running.Remove(item);
                // 끝나는 중(OnComplete 안)인 시퀀스는 자동으로 지워지므로 다시 끄지 않는다.
                if (running != null && running.IsActive() && !running.IsComplete())
                {
                    running.Kill(false);
                }
            }
        }

        public static void ReturnBit(SpriteRenderer bit)
        {
            // 이미 돌려놓은 조각(LiveBits에 없음)은 다시 넣지 않는다.
            if (bit == null || !LiveBits.Remove(bit))
            {
                return;
            }

            StopRunning(bit);
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

            Text label = null;
            while (Labels.Count > 0 && label == null)
            {
                label = Labels.Dequeue();
            }

            if (label == null)
            {
                label = UiFonts.CreateText(_labelRoot, "FXFloat", 22, TextAnchor.MiddleCenter);
                var rect = label.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(240f, 48f);
                if (!label.TryGetComponent<CanvasGroup>(out _))
                {
                    label.gameObject.AddComponent<CanvasGroup>();
                }
            }

            label.gameObject.SetActive(true);
            LiveLabels.Add(label);
            return label;
        }

        public static void ReturnLabel(Text label)
        {
            if (label == null || !LiveLabels.Remove(label))
            {
                return;
            }

            StopRunning(label);
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

            for (var i = LiveLabels.Count - 1; i >= 0; i--)
            {
                ReturnLabel(LiveLabels[i]);
            }
        }

        static void DropDestroyedBits()
        {
            for (var i = LiveBits.Count - 1; i >= 0; i--)
            {
                if (LiveBits[i] == null)
                {
                    LiveBits.RemoveAt(i);
                }
            }

            LiveLabels.RemoveAll(label => label == null);
            var gone = new List<Component>();
            foreach (var item in Running.Keys)
            {
                if (item == null)
                {
                    gone.Add(item);
                }
            }

            foreach (var item in gone)
            {
                Running.Remove(item);
            }

            var count = Bits.Count;
            for (var i = 0; i < count; i++)
            {
                var bit = Bits.Dequeue();
                if (bit != null)
                {
                    Bits.Enqueue(bit);
                }
            }
        }
    }
}
