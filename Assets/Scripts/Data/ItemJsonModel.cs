using System;
using System.Collections.Generic;

namespace ScrapFishing.Data
{
    [Serializable]
    public class ItemJsonRoot
    {
        public int spriteSize;
        public int chipToGoldRatio;
        public List<ItemJson> items;
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
