using System.Collections.Generic;
using UnityEngine;

namespace ScrapFishing.Gacha
{
    // Resources/Gear의 기준점 JSON 3개. 좌표는 이미지 픽셀(원점 왼쪽 위, y 아래로 증가)이다.
    // 낚싯대는 128 아이콘 기준, 보트·피셔는 256 레이어 기준.
    public static class GearAnchors
    {
        public const string RodResource = "Gear/Rod/rod_anchors";
        public const string BoatResource = "Gear/Boat/Boat/boat_anchors";
        public const string FisherResource = "Gear/Fisher/fisher_anchors";
        public const string LureResource = "Gear/Lure/lure_anchors";

        public struct Rod
        {
            public Vector2 HandleEnd;
            public Vector2 Tip;
        }

        // Top·Bottom은 보트 이미지에서 불투명한 부분의 위아래(bbox y0·y1)다.
        public struct Boat
        {
            public float BottomY;
            public float DeckY;
            public float Top;
            public float Bottom;
        }

        public struct Fisher
        {
            public float WaistY;
            public Vector2 Hand;
        }

        // 피셔 256 레이어의 머리 꼭대기(발끝 footY − 몸 높이 bodyHeight, 가로는 발 중심 footX).
        public static Vector2 FisherHead { get; private set; } = new Vector2(110f, 80f);
        // 피셔 256 레이어의 발끝 행(가장 아래 불투명 행).
        public static float FisherFootY { get; private set; } = 210f;

        static Dictionary<string, Rod> _rods;
        static Dictionary<string, Boat> _boats;
        static Dictionary<string, Fisher> _fishers;
        static Dictionary<string, Vector2> _lureEyes;

        public static IReadOnlyDictionary<string, Rod> Rods
        {
            get
            {
                Ensure();
                return _rods;
            }
        }

        public static IReadOnlyDictionary<string, Boat> Boats
        {
            get
            {
                Ensure();
                return _boats;
            }
        }

        public static IReadOnlyDictionary<string, Fisher> Fishers
        {
            get
            {
                Ensure();
                return _fishers;
            }
        }

        // 루어 128 이미지에서 낚싯줄이 묶이는 맨 위 고리.
        public static IReadOnlyDictionary<string, Vector2> LureEyes
        {
            get
            {
                Ensure();
                return _lureEyes;
            }
        }

        public static void Reload()
        {
            _rods = null;
            Ensure();
        }

        static void Ensure()
        {
            if (_rods != null)
            {
                return;
            }

            _rods = new Dictionary<string, Rod>();
            _boats = new Dictionary<string, Boat>();
            _fishers = new Dictionary<string, Fisher>();
            _lureEyes = new Dictionary<string, Vector2>();

            foreach (var pair in Entries(Load(RodResource)))
            {
                _rods[pair.Key] = new Rod { HandleEnd = Point(pair.Value, "handleEnd"), Tip = Point(pair.Value, "tip") };
            }

            foreach (var pair in Entries(Load(BoatResource)))
            {
                var box = pair.Value.TryGetValue("bbox", out var raw) ? raw as List<object> : null;
                var top = box != null && box.Count >= 4 && box[1] is double y0 ? (float)y0 : 0f;
                var bottom = box != null && box.Count >= 4 && box[3] is double y1 ? (float)y1 : Number(pair.Value, "bottomY");
                _boats[pair.Key] = new Boat
                {
                    BottomY = Number(pair.Value, "bottomY"),
                    DeckY = Number(pair.Value, "deckY"),
                    Top = top,
                    Bottom = bottom
                };
            }

            var fisherRoot = Load(FisherResource);
            if (fisherRoot != null)
            {
                var footX = Number(fisherRoot, "footX");
                var footY = Number(fisherRoot, "footY");
                var body = Number(fisherRoot, "bodyHeight");
                if (footY > 0f && body > 0f)
                {
                    FisherHead = new Vector2(footX, footY - body);
                    FisherFootY = footY;
                }
            }

            var fishers = fisherRoot != null && fisherRoot.TryGetValue("fishers", out var list) ? list as Dictionary<string, object> : null;
            foreach (var pair in Entries(fishers))
            {
                _fishers[pair.Key] = new Fisher { WaistY = Number(pair.Value, "waistY"), Hand = Point(pair.Value, "handPos") };
            }

            foreach (var pair in Entries(Load(LureResource)))
            {
                _lureEyes[pair.Key] = Point(pair.Value, "eye");
            }
        }

        static Dictionary<string, object> Load(string resource)
        {
            var asset = Resources.Load<TextAsset>(resource);
            if (asset == null)
            {
                Debug.LogWarning("기준점 JSON이 없습니다: Resources/" + resource);
                return null;
            }

            return MiniJson.Parse(asset.text) as Dictionary<string, object>;
        }

        // "_설명" 같은 메모 키는 건너뛴다.
        static IEnumerable<KeyValuePair<string, Dictionary<string, object>>> Entries(Dictionary<string, object> map)
        {
            if (map == null)
            {
                yield break;
            }

            foreach (var pair in map)
            {
                if (!pair.Key.StartsWith("_") && pair.Value is Dictionary<string, object> value)
                {
                    yield return new KeyValuePair<string, Dictionary<string, object>>(pair.Key, value);
                }
            }
        }

        static float Number(Dictionary<string, object> map, string key)
        {
            return map.TryGetValue(key, out var value) && value is double number ? (float)number : 0f;
        }

        static Vector2 Point(Dictionary<string, object> map, string key)
        {
            if (map.TryGetValue(key, out var value) && value is List<object> list && list.Count >= 2
                && list[0] is double x && list[1] is double y)
            {
                return new Vector2((float)x, (float)y);
            }

            return Vector2.zero;
        }
    }
}
