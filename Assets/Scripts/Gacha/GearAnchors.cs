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

        public struct Rod
        {
            public Vector2 HandleEnd;
            public Vector2 Tip;
        }

        public struct Boat
        {
            public float BottomY;
            public float DeckY;
        }

        public struct Fisher
        {
            public float WaistY;
            public Vector2 Hand;
        }

        static Dictionary<string, Rod> _rods;
        static Dictionary<string, Boat> _boats;
        static Dictionary<string, Fisher> _fishers;

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

            foreach (var pair in Entries(Load(RodResource)))
            {
                _rods[pair.Key] = new Rod { HandleEnd = Point(pair.Value, "handleEnd"), Tip = Point(pair.Value, "tip") };
            }

            foreach (var pair in Entries(Load(BoatResource)))
            {
                _boats[pair.Key] = new Boat { BottomY = Number(pair.Value, "bottomY"), DeckY = Number(pair.Value, "deckY") };
            }

            var fisherRoot = Load(FisherResource);
            var fishers = fisherRoot != null && fisherRoot.TryGetValue("fishers", out var list) ? list as Dictionary<string, object> : null;
            foreach (var pair in Entries(fishers))
            {
                _fishers[pair.Key] = new Fisher { WaistY = Number(pair.Value, "waistY"), Hand = Point(pair.Value, "handPos") };
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
