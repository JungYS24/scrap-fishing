using System;
using System.Collections.Generic;
using ScrapFishing.Data;
using UnityEngine;

namespace ScrapFishing.Gacha
{
    public sealed class GachaCatalog
    {
        public const string ResourcesFolder = "Gacha";

        readonly List<GachaMachineData> _machines = new List<GachaMachineData>();
        readonly Dictionary<string, GearData> _gears = new Dictionary<string, GearData>();
        readonly Dictionary<string, GachaMachineData> _machineOfGear = new Dictionary<string, GachaMachineData>();
        readonly List<GearData> _allGears = new List<GearData>();

        public IReadOnlyList<GachaMachineData> Machines => _machines;
        // 전체 머신 장비(머신 순서). 축제·일요일 데일리의 풀.
        public IReadOnlyList<GearData> AllGears => _allGears;
        // 배너 파일이 없으면 머신마다 상시 배너 하나씩.
        public GachaBannerSet BannerSet { get; private set; }
        public IReadOnlyList<GachaBanner> Banners => BannerSet.Banners;

        public GachaCatalog(IEnumerable<GachaMachineData> machines)
        {
            if (machines == null)
            {
                BannerSet = new GachaBannerSet();
                return;
            }

            foreach (var machine in machines)
            {
                if (machine == null)
                {
                    continue;
                }

                _machines.Add(machine);
                foreach (var gear in machine.Gears)
                {
                    if (gear == null || string.IsNullOrEmpty(gear.Id) || _gears.ContainsKey(gear.Id))
                    {
                        continue;
                    }

                    _gears.Add(gear.Id, gear);
                    _machineOfGear.Add(gear.Id, machine);
                    _allGears.Add(gear);
                }
            }

            BannerSet = GachaBannerSet.FromMachines(_machines);
        }

        public void SetBanners(GachaBannerSet set)
        {
            if (set != null && set.Banners.Count > 0)
            {
                BannerSet = set;
            }
        }

        // Resources/Gacha 에 머신 에셋이 있으면 그것을, 없으면 oripa_rewards.json을, 그것도 없으면 코드 기본값을 쓴다.
        // 배너는 oripa_banners.json을 읽고, 없거나 깨졌으면 머신별 상시 배너만 둔다.
        public static GachaCatalog LoadDefault()
        {
            var assets = Resources.LoadAll<GachaMachineData>(ResourcesFolder);
            GachaCatalog catalog;
            if (assets != null && assets.Length > 0)
            {
                catalog = new GachaCatalog(assets);
            }
            else
            {
                var fromJson = OripaCatalogJson.Load();
                catalog = new GachaCatalog(fromJson != null && fromJson.Length > 0 ? fromJson : GachaDefaults.CreateMachines());
            }

            catalog.SetBanners(OripaBannerJson.Load(catalog));
            return catalog;
        }

        public GachaMachineData FindMachine(string machineId)
        {
            for (var i = 0; i < _machines.Count; i++)
            {
                if (_machines[i].Id == machineId)
                {
                    return _machines[i];
                }
            }

            return null;
        }

        public GearData FindGear(string gearId)
        {
            return gearId != null && _gears.TryGetValue(gearId, out var gear) ? gear : null;
        }

        public GachaMachineData MachineForSlot(GearSlot slot)
        {
            for (var i = 0; i < _machines.Count; i++)
            {
                if (_machines[i].Slot == slot)
                {
                    return _machines[i];
                }
            }

            return null;
        }

        // 카탈로그 안에서 그 등급 장비가 쓰는 환원 배수. 없으면 등급 기본값.
        public float RefundMultiplier(ItemRarity rarity)
        {
            foreach (var gear in _gears.Values)
            {
                if (gear.Rarity == rarity)
                {
                    return gear.RefundMultiplier;
                }
            }

            return GearData.DefaultRefundMultiplier(rarity);
        }

        public GachaMachineData MachineOf(string gearId)
        {
            return gearId != null && _machineOfGear.TryGetValue(gearId, out var machine) ? machine : null;
        }

        public int RefundValue(GearData gear)
        {
            if (gear == null)
            {
                return 0;
            }

            var machine = MachineOf(gear.Id);
            var price = machine != null ? machine.Price : 0;
            return Math.Max(0, (int)Math.Round(price * gear.RefundMultiplier, MidpointRounding.AwayFromZero));
        }
    }

    public static class GachaDefaults
    {
        public const int LurePrice = 5000;
        public const int BoatPrice = 7500;
        public const int DronePrice = 7500;
        public const int HelmetPrice = 5000;

        public static GachaMachineData[] CreateMachines()
        {
            return new[]
            {
                Machine("machine_lure", "루어 머신", GearSlot.Lure, LurePrice,
                    GearData.Create("lure_rusty_magnet", "녹슨 자석 루어", GearSlot.Lure, ItemRarity.Normal,
                        new GearEffect(GearStat.LureCapacity, 1)),
                    GearData.Create("lure_neon_bait", "네온 미끼 루어", GearSlot.Lure, ItemRarity.Rare,
                        new GearEffect(GearStat.LureCapacity, 2),
                        new GearEffect(GearStat.CatchRadiusPercent, 10)),
                    GearData.Create("lure_drone_core", "드론 코어 루어", GearSlot.Lure, ItemRarity.Epic,
                        new GearEffect(GearStat.LureCapacity, 3),
                        new GearEffect(GearStat.RareChancePercent, 5)),
                    GearData.Create("lure_kraken_tentacle", "크라켄 촉수 루어", GearSlot.Lure, ItemRarity.Legend,
                        new GearEffect(GearStat.LureCapacity, 4),
                        new GearEffect(GearStat.RareChancePercent, 12))),
                Machine("machine_boat", "보트 머신", GearSlot.Boat, BoatPrice,
                    GearData.Create("boat_patched_barge", "덧댄 철판 바지선", GearSlot.Boat, ItemRarity.Normal,
                        new GearEffect(GearStat.SellPercent, 3)),
                    GearData.Create("boat_neon_barge", "네온 간판 바지선", GearSlot.Boat, ItemRarity.Rare,
                        new GearEffect(GearStat.SellPercent, 6),
                        new GearEffect(GearStat.GaugeSpeedPercent, -10)),
                    GearData.Create("boat_shinzen_wreck", "신젠 폐선 개조선", GearSlot.Boat, ItemRarity.Epic,
                        new GearEffect(GearStat.SellPercent, 10),
                        new GearEffect(GearStat.GaugeSpeedPercent, -15)),
                    GearData.Create("boat_mecha_fortress", "메카 잔해 요새선", GearSlot.Boat, ItemRarity.Legend,
                        new GearEffect(GearStat.SellPercent, 18),
                        new GearEffect(GearStat.EndBonusPercent, 10))),
                Machine("machine_drone", "드론 머신", GearSlot.Drone, DronePrice,
                    GearData.Create("drone_old_watch", "구형 감시 드론", GearSlot.Drone, ItemRarity.Normal,
                        new GearEffect(GearStat.DroneRadiusPercent, 10)),
                    GearData.Create("drone_search", "수색 드론", GearSlot.Drone, ItemRarity.Rare,
                        new GearEffect(GearStat.DroneRadiusPercent, 20),
                        new GearEffect(GearStat.HazardWarning, 1)),
                    GearData.Create("drone_recovery", "회수 드론", GearSlot.Drone, ItemRarity.Epic,
                        new GearEffect(GearStat.DroneRadiusPercent, 30),
                        new GearEffect(GearStat.AutoCollectSeconds, 10)),
                    GearData.Create("drone_ai_prototype", "프로토타입 AI 드론", GearSlot.Drone, ItemRarity.Legend,
                        new GearEffect(GearStat.DroneRadiusPercent, 40),
                        new GearEffect(GearStat.AutoCollectSeconds, 6),
                        new GearEffect(GearStat.HazardShield, 1))),
                Machine("machine_helmet", "헬멧 머신", GearSlot.Helmet, HelmetPrice,
                    GearData.Create("suit_raincoat", "방수 우비", GearSlot.Helmet, ItemRarity.Normal,
                        new GearEffect(GearStat.OxygenPercent, 10)),
                    GearData.Create("suit_anticorrosion", "침식 방지 코팅", GearSlot.Helmet, ItemRarity.Rare,
                        new GearEffect(GearStat.OxygenPercent, 20),
                        new GearEffect(GearStat.HazardDamageReductionPercent, 25)),
                    GearData.Create("suit_cyber_diver", "사이버 잠수복", GearSlot.Helmet, ItemRarity.Epic,
                        new GearEffect(GearStat.OxygenPercent, 30),
                        new GearEffect(GearStat.HazardDamageReductionPercent, 50)),
                    GearData.Create("suit_abyss_exo", "심연 엑소슈트", GearSlot.Helmet, ItemRarity.Legend,
                        new GearEffect(GearStat.OxygenPercent, 45),
                        new GearEffect(GearStat.HazardDamageReductionPercent, 100)))
            };
        }

        static GachaMachineData Machine(string id, string name, GearSlot slot, int price, params GearData[] gears)
        {
            var machine = ScriptableObject.CreateInstance<GachaMachineData>();
            machine.Configure(id, name, slot, price, gears);
            return machine;
        }
    }
}
