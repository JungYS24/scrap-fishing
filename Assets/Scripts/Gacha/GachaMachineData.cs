using System.Collections.Generic;
using ScrapFishing.Data;
using UnityEngine;

namespace ScrapFishing.Gacha
{
    [CreateAssetMenu(menuName = "ScrapFishing/Gacha/GachaMachineData")]
    public class GachaMachineData : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField] string englishName;
        [SerializeField] string bannerCopy;
        [SerializeField] GearSlot slot;
        [SerializeField] int price = 5000;

        [Header("등급 확률 (%)")]
        [SerializeField] float normalRate = 70f;
        [SerializeField] float rareRate = 22f;
        [SerializeField] float epicRate = 7f;
        [SerializeField] float legendRate = 1f;
        [Tooltip("싱크로 확률 (%). 확률 보간을 받지 않는다. 0이면 나오지 않는다.")]
        [SerializeField] float synchroRate;

        [Header("확률 보간 (천장 없음)")]
        [Tooltip("Legend 없이 뽑은 횟수가 이 주기에 닿으면 카운트를 0으로 되돌린다. 확정 당첨은 없다.")]
        [SerializeField] int boostCycle = 101;
        [Tooltip("주기 안에서 이 번째 뽑기부터 Legend 확률을 올리기 시작한다.")]
        [SerializeField] int boostStart = 51;
        [Tooltip("주기 마지막 뽑기의 Legend 확률 (%). 시작 지점부터 선형 보간한다.")]
        [SerializeField] float boostLegendRate = 5f;

        [SerializeField] List<GearData> gears = new List<GearData>();
        [SerializeField] Sprite banner;

        public string Id => id;
        public string DisplayName => displayName;
        public string EnglishName => englishName;
        public string BannerCopy => bannerCopy;
        public GearSlot Slot => slot;
        public int Price => price;
        public int BoostCycle => boostCycle;
        public int BoostStart => boostStart;
        public float BoostLegendRate => boostLegendRate;
        public IReadOnlyList<GearData> Gears => gears;
        public Sprite Banner => banner;

        public float BaseRate(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Rare: return rareRate;
                case ItemRarity.Epic: return epicRate;
                case ItemRarity.Legend: return legendRate;
                case ItemRarity.Synchro: return synchroRate;
                default: return normalRate;
            }
        }

        public void Configure(string machineId, string name, GearSlot machineSlot, int onePrice, IEnumerable<GearData> machineGears)
        {
            id = machineId;
            this.name = machineId;
            displayName = name;
            slot = machineSlot;
            price = onePrice;
            gears = new List<GearData>(machineGears ?? new GearData[0]);
        }

        public void SetRates(float normal, float rare, float epic, float legend, float synchro = 0f)
        {
            normalRate = normal;
            rareRate = rare;
            epicRate = epic;
            legendRate = legend;
            synchroRate = synchro;
        }

        public void SetCopy(string english, string copy)
        {
            englishName = english;
            bannerCopy = copy;
        }

        public void SetBoost(int cycle, int start, float legendAtCycleEnd)
        {
            boostCycle = cycle;
            boostStart = start;
            boostLegendRate = legendAtCycleEnd;
        }
    }
}
