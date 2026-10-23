using System;
using System.Collections.Generic;
using ScrapFishing.Data;
using ScrapFishing.Fishing;
using ScrapFishing.Gacha;
using ScrapFishing.Scrap;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScrapFishing.Core
{
    public class RunSession : MonoBehaviour
    {
        public const float Duration = 60f;
        public const int BaseCastsPerDive = 3;
        public const float MinDiveSeconds = 8f;

        // 아이템 gold는 chip의 1/20. 달러 = CY + gold × 이 값이면, 보트 배율 1일 때 골드가 한 판 수입의 20%다.
        // 배율이 오르면 CY만 커져서 비율은 내려가고, 만렙 보트(×1.6)에서도 약 13~16%로 15% 근처에 남는다.
        public const int GoldToDollarRate = 5;

        const string BestKey = "BestCY";

        public float CastDepth { get; private set; }
        public float DeepestCast { get; private set; }
        public int CY { get; private set; }
        public int Gold { get; private set; }
        public int BestCY { get; private set; }
        public bool IsNewBest { get; private set; }
        public int LastGain { get; private set; }
        public int EarnedDollars { get; private set; }
        // 보트 장비(판 종료 보너스 %)로 더 받은 달러. EarnedDollars에 포함된다.
        public int GearBonusDollars { get; private set; }
        // 피셔 장비(diveEveryCastsBonus)로 줄어든 잠수 주기. 판을 시작할 때 정한다.
        public int CastsPerDive { get; private set; } = BaseCastsPerDive;
        public int GoldDollars => Mathf.Max(0, Gold) * GoldToDollarRate;
        public int HazardCount => _hazardCount;
        public IReadOnlyList<LootStack> Loot => _loot;
        public float Elapsed { get; private set; }
        public IReadOnlyList<ScrapDefinition> Caught => _caught;
        public float Remaining => Mathf.Max(0f, Duration - Elapsed);
        public bool IsExpired => _running && Elapsed >= Duration;
        public bool IsRunning => _running;
        public int DiveProgress { get; private set; }
        public bool HasDiveCharge => DiveProgress >= CastsPerDive;
        public int CastsUntilDive => CastsPerDive - DiveProgress;
        public bool HasDiveTime => Remaining >= MinDiveSeconds;
        public bool CanDive => HasDiveCharge && HasDiveTime;

        public event Action CaughtItem;
        public event Action<ScrapDefinition, Vector3, int> CaughtAt;
        public event Action DiveCharged;

        readonly List<ScrapDefinition> _caught = new List<ScrapDefinition>();
        readonly List<LootStack> _loot = new List<LootStack>();
        int _hazardCount;
        bool _running;
        float _chipMultiplier = 1f;

        void Start()
        {
            Time.timeScale = 0f;
        }

        public void StartRun()
        {
            CastDepth = 0f;
            DeepestCast = 0f;
            CY = 0;
            Gold = 0;
            IsNewBest = false;
            LastGain = 0;
            EarnedDollars = 0;
            GearBonusDollars = 0;
            Elapsed = 0f;
            DiveProgress = 0;
            CastsPerDive = Mathf.Max(1, BaseCastsPerDive - GearBonuses.Count(GearStat.DiveEveryCastsBonus));
            _running = true;
            _chipMultiplier = Progression.ChipMultiplier * GearBonuses.Scale(GearStat.SellPercent);
            _caught.Clear();
            _loot.Clear();
            _hazardCount = 0;
            Time.timeScale = 1f;
        }

        public void EndRun()
        {
            _running = false;
            var baseDollars = CY + GoldDollars;
            GearBonusDollars = Mathf.Max(0, Mathf.RoundToInt(baseDollars * Mathf.Max(0f, GearBonuses.Get(GearStat.EndBonusPercent)) / 100f));
            EarnedDollars = baseDollars + GearBonusDollars;
            Progression.AddDollars(EarnedDollars);
            BestCY = PlayerPrefs.GetInt(BestKey, 0);
            IsNewBest = CY > BestCY;
            if (IsNewBest)
            {
                BestCY = CY;
                PlayerPrefs.SetInt(BestKey, BestCY);
                PlayerPrefs.Save();
            }

            Time.timeScale = 0f;
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void RegisterCast(float depth)
        {
            CastDepth = Mathf.Clamp01(depth);
            if (CastDepth > DeepestCast)
            {
                DeepestCast = CastDepth;
            }
        }

        public void RegisterCastComplete()
        {
            if (HasDiveCharge)
            {
                return;
            }

            DiveProgress++;
            if (HasDiveCharge)
            {
                DiveCharged?.Invoke();
            }
        }

        public void MarkDived()
        {
            DiveProgress = 0;
        }

        public void Tick(float deltaTime)
        {
            if (!_running)
            {
                return;
            }

            Elapsed = Mathf.Min(Duration, Elapsed + deltaTime);
        }

        public void AddCY(int amount)
        {
            CY = Mathf.Max(0, CY + amount);
        }

        public void AddReward(ItemData d)
        {
            if (d == null)
            {
                return;
            }

            AddCY(d.Chip);
            Gold = Mathf.Max(0, Gold + d.Gold);
        }

        public void AddSettledCatch(ScrapDefinition definition, ItemData data, Vector3 worldPos, out int chip, out int gold)
        {
            chip = 0;
            gold = 0;
            if (definition == null && data == null)
            {
                return;
            }

            if (data != null)
            {
                chip = Mathf.RoundToInt(data.Chip * _chipMultiplier);
                gold = data.Gold;
                if (definition == null)
                {
                    definition = ScrapDefinition.CreateRuntime(data.DisplayName, ScrapGrade.Common, data.Chip, Color.white);
                }
            }
            else
            {
                chip = Mathf.RoundToInt(definition.Value * _chipMultiplier);
                gold = LureLoad.GoldFromAmount(definition.Value);
            }

            _caught.Add(definition);
            var signed = data != null ? data.Chip : definition.Value;
            RememberLoot(data, definition, signed);
            LastGain = chip;
            AddCY(chip);
            if (!LureLoad.IsHazard(signed))
            {
                Gold = Mathf.Max(0, Gold + gold);
            }
            CaughtItem?.Invoke();
            CaughtAt?.Invoke(definition, worldPos, LastGain);
        }

        public void AddCatch(ScrapDefinition definition)
        {
            AddCatch(definition, Vector3.zero);
        }

        public void AddCatch(ScrapDefinition definition, Vector3 worldPos)
        {
            AddCatch(definition, null, worldPos);
        }

        public void AddCatch(ScrapDefinition definition, ItemData data, Vector3 worldPos)
        {
            if (definition == null && data == null)
            {
                return;
            }

            if (definition == null)
            {
                definition = ScrapDefinition.FromItem(data);
            }

            _caught.Add(definition);
            RememberLoot(data, definition, data != null ? data.Chip : definition.Value);
            LastGain = Mathf.RoundToInt(definition.Value * _chipMultiplier);
            AddCY(LastGain);
            if (data != null && !LureLoad.IsHazard(data.Chip))
            {
                Gold = Mathf.Max(0, Gold + data.Gold);
            }

            CaughtItem?.Invoke();
            CaughtAt?.Invoke(definition, worldPos, LastGain);
        }

        void RememberLoot(ItemData data, ScrapDefinition definition, int signedChip)
        {
            if (LureLoad.IsHazard(signedChip))
            {
                _hazardCount++;
                return;
            }

            var key = data != null && !string.IsNullOrEmpty(data.Id)
                ? data.Id
                : definition != null ? definition.DisplayName : "scrap";
            for (var i = 0; i < _loot.Count; i++)
            {
                if (_loot[i].Key != key)
                {
                    continue;
                }

                _loot[i].Count++;
                return;
            }

            _loot.Add(new LootStack(key, data, definition));
            _loot.Sort((a, b) => b.Rarity.CompareTo(a.Rarity));
        }

#if UNITY_EDITOR
        public void PreviewResults(int cy, int gold, bool newBest, ItemData[] items, int hazards)
        {
            CY = Mathf.Max(0, cy);
            Gold = Mathf.Max(0, gold);
            EarnedDollars = CY + GoldDollars;
            IsNewBest = newBest;
            BestCY = newBest ? CY : Mathf.Max(0, CY / 2);
            _caught.Clear();
            _loot.Clear();
            _hazardCount = 0;
            if (items != null)
            {
                for (var i = 0; i < items.Length; i++)
                {
                    var data = items[i];
                    if (data == null)
                    {
                        continue;
                    }

                    var definition = ScrapDefinition.FromItem(data);
                    _caught.Add(definition);
                    RememberLoot(data, definition, data.Chip);
                }
            }

            _hazardCount += Mathf.Max(0, hazards);
        }
#endif

        public ScrapDefinition HighestGrade()
        {
            ScrapDefinition best = null;
            foreach (var scrap in _caught)
            {
                if (best == null || scrap.Grade > best.Grade)
                {
                    best = scrap;
                }
            }

            return best;
        }

        public sealed class LootStack
        {
            public string Key { get; }
            public ItemData Data { get; }
            public ScrapDefinition Definition { get; }
            public int Count;
            public ItemRarity Rarity { get; }
            public string Name => Data != null ? Data.DisplayName : Definition != null ? Definition.DisplayName : "scrap";
            public Sprite Icon => Data != null && Data.Icon != null ? Data.Icon : Definition != null ? Definition.Sprite : null;
            public Color Placeholder => Definition != null ? Definition.PlaceholderColor : Color.white;

            public LootStack(string key, ItemData data, ScrapDefinition definition)
            {
                Key = key;
                Data = data;
                Definition = definition;
                Count = 1;
                Rarity = data != null ? data.Rarity : RarityFrom(definition);
            }

            static ItemRarity RarityFrom(ScrapDefinition definition)
            {
                if (definition == null)
                {
                    return ItemRarity.Normal;
                }

                switch (definition.Grade)
                {
                    case ScrapGrade.Rare:
                        return ItemRarity.Rare;
                    case ScrapGrade.Mutant:
                        return ItemRarity.Epic;
                    default:
                        return ItemRarity.Normal;
                }
            }
        }
    }
}
