using System.Collections.Generic;
using UnityEngine;

namespace ScrapFishing.Gacha
{
    // 장비 외형의 공통 규칙: 장착 id(없으면 {슬롯}_normal_01) → Resources 경로 → 스프라이트.
    // 루어 스킨·낚싯대·보트·피셔 레이어·드론·헬멧이 모두 이 함수를 쓴다. 외형만 다루고 효과는 GearBonuses가 다룬다.
    public static class GearVisuals
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        static readonly HashSet<string> Missing = new HashSet<string>();

        public static string DefaultId(GearSlot slot)
        {
            return slot.ToString().ToLowerInvariant() + "_normal_01";
        }

        public static string EquippedOrDefaultId(GearSlot slot)
        {
            var equipped = GearBonuses.Equipped(slot);
            return equipped != null ? equipped.Id : DefaultId(slot);
        }

        // 가챠 UI 아이콘(128). 보트·피셔는 아이콘 폴더가 따로 있다.
        public static string IconPath(GearSlot slot, string id)
        {
            switch (slot)
            {
                case GearSlot.Boat: return "Gear/Boat/BoatIcon/" + id;
                case GearSlot.Fisher: return "Gear/Fisher/FisherIcon/" + id;
                default: return "Gear/" + slot + "/" + id;
            }
        }

        // 게임 화면용 외형. 보트·피셔는 256 레이어, 나머지(루어·낚싯대·드론·헬멧)는 아이콘과 같은 128 이미지다.
        public static string WorldPath(GearSlot slot, string id)
        {
            switch (slot)
            {
                case GearSlot.Boat: return "Gear/Boat/Boat/" + id;
                case GearSlot.Fisher: return "Gear/Fisher/Fisher/" + id;
                default: return IconPath(slot, id);
            }
        }

        public static Sprite EquippedWorldSprite(GearSlot slot)
        {
            return Load(WorldPath(slot, EquippedOrDefaultId(slot)));
        }

        // Multiple로 잘려 임포트된 경우(이름_0)도 찾는다. 못 찾으면 경로마다 한 번만 경고한다.
        public static Sprite Load(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            if (Cache.TryGetValue(path, out var cached) && cached != null)
            {
                return cached;
            }

            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                var sprites = Resources.LoadAll<Sprite>(path);
                sprite = sprites != null && sprites.Length > 0 ? sprites[0] : null;
            }

            if (sprite != null)
            {
                Cache[path] = sprite;
            }
            else if (Missing.Add(path))
            {
                Debug.LogWarning("장비 이미지를 찾지 못했습니다: Resources/" + path);
            }

            return sprite;
        }
    }
}
