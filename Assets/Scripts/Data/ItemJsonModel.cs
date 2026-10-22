using System;
using System.Collections.Generic;

namespace ScrapFishing.Data
{
    [Serializable]
    public class ItemJsonRoot
    {
        public int spriteSize;
        public int chipToGoldRatio;
        public List<ZoneRarityJson> zoneRarity;
        public List<ItemJson> items;
    }

    [Serializable]
    public class ZoneRarityJson
    {
        public int index;
        public string name;
        public int normal;
        public int rare;
        public int epic;
        public int legend;
        public int synchro;
    }

    [Serializable]
    public class ItemJson
    {
        public string id;
        public string name;
        public string category;
        public string zone;
        public string rarity;
        public string sprite;
        // 스프라이트가 아직 없을 때 대신 쓸 아이템 id. 아트가 들어오면 비워 둔다.
        public string placeholder;
        public bool hasAnimation;
        public AnimJson animation;
        public int chip;
        public int gold;
    }

    [Serializable]
    public class AnimJson
    {
        public string clip;
        public string controller;
        public int frames;
        public bool loop;
    }
}
