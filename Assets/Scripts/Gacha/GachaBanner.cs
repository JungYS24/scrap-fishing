using System;
using System.Collections.Generic;
using ScrapFishing.Data;
using UnityEngine;

namespace ScrapFishing.Gacha
{
    public enum BannerType
    {
        Standard,
        Pickup,
        Daily,
        Festival,
    }

    public enum PickupKind
    {
        None,
        Legend,
        Synchro,
    }

    // 배너 = 어떤 풀에서, 어떤 확률로, 얼마에, 몇 번 뽑는지. 장비·등급·환원 배율은 머신(oripa_rewards.json)을 따른다.
    public sealed class GachaBanner
    {
        public string Id;
        public BannerType Type;
        public int Order;
        // standard·pickup의 풀과 천장 카운터 머신. daily·festival은 null.
        public GachaMachineData Machine;
        public string NameKo;
        public string NameEn;
        public string DescKo;
        public int Price;
        public int[] PullCounts = { 1, GachaService.TenPull };

        public PickupKind Pickup;
        public GearData Featured;
        // 레전드 픽업: 레전드가 나왔을 때 픽업 장비가 될 확률.
        public float FeaturedShare = 1f;
        // 싱크로 픽업: 레전드·싱크로 기본 확률(0~1). 노멀·레어·에픽은 머신 비율대로 나머지를 나눈다.
        public float LegendRate = -1f;
        public float SynchroRate = -1f;

        // 축제: 등급 확률(합 1), 평생 횟수 제한, N번째 뽑기 확정.
        public float[] GradeRates;
        public int MaxPulls;
        public int PityAt;
        public ItemRarity PityMin = ItemRarity.Legend;

        // 데일리: 모든 뽑기의 최소 등급, 요일별 풀(값이 null이면 전체 머신).
        public ItemRarity GuaranteeMin = ItemRarity.Normal;
        public readonly Dictionary<DayOfWeek, GachaMachineData> WeekdayPool = new Dictionary<DayOfWeek, GachaMachineData>();

        // 배너 전용 아트(Resources 경로). 아직 없으면 비워 두고 머신 카드 디자인을 쓴다.
        public string BannerImage;
        Sprite _bannerSprite;
        bool _bannerLoaded;

        public bool Offers(int count)
        {
            return PullCounts != null && Array.IndexOf(PullCounts, count) >= 0;
        }

        public Sprite BannerSprite
        {
            get
            {
                if (!_bannerLoaded)
                {
                    _bannerLoaded = true;
                    _bannerSprite = string.IsNullOrEmpty(BannerImage) ? null : Resources.Load<Sprite>(BannerImage);
                }

                return _bannerSprite;
            }
        }

        public static GachaBanner Standard(GachaMachineData machine, int order)
        {
            return new GachaBanner
            {
                Id = "banner_" + GachaVideoConfig.KeyOf(machine.Id) + "_standard",
                Type = BannerType.Standard,
                Order = order,
                Machine = machine,
                NameKo = machine.DisplayName,
                NameEn = machine.EnglishName,
                DescKo = machine.BannerCopy,
                Price = machine.Price,
            };
        }
    }

    // 배너 목록과 공통 설정. 로테이션은 필드만 읽어 두고 아직 쓰지 않는다(전부 상시 노출).
    public sealed class GachaBannerSet
    {
        public const int KstOffsetHours = 9;

        public readonly List<GachaBanner> Banners = new List<GachaBanner>();
        public int UtcOffsetHours = KstOffsetHours;
        public int DailyResetHour = 5;
        public bool RotationEnabled;
        public int RotationActiveCount;
        public int RotationPeriodDays;

        public static GachaBannerSet FromMachines(IReadOnlyList<GachaMachineData> machines)
        {
            var set = new GachaBannerSet();
            for (var i = 0; i < machines.Count; i++)
            {
                set.Banners.Add(GachaBanner.Standard(machines[i], i + 1));
            }

            return set;
        }

        public GachaBanner Find(string id)
        {
            foreach (var banner in Banners)
            {
                if (banner.Id == id)
                {
                    return banner;
                }
            }

            return null;
        }

        // 리셋 시각 기준 날짜. 기기 시간대와 상관없이 UTC에 고정 오프셋을 더한다(서울은 서머타임이 없다).
        public DateTime LocalDay(DateTime utc)
        {
            return utc.AddHours(UtcOffsetHours - DailyResetHour).Date;
        }
    }

    // Resources/Gacha/oripa_banners.json을 배너로 바꾼다. 머신·장비는 카탈로그에서 찾는다.
    public static class OripaBannerJson
    {
        public const string Resource = "Gacha/oripa_banners";

        static readonly string[] Weekdays = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

        public static GachaBannerSet Load(GachaCatalog catalog)
        {
            var asset = Resources.Load<TextAsset>(Resource);
            if (asset == null)
            {
                return null;
            }

            try
            {
                return Parse(asset.text, catalog);
            }
            catch (Exception e)
            {
                Debug.LogError("oripa_banners.json을 읽지 못했습니다: " + e.Message);
                return null;
            }
        }

        public static GachaBannerSet Parse(string json, GachaCatalog catalog)
        {
            var root = MiniJson.Parse(json) as Dictionary<string, object>;
            if (root == null)
            {
                throw new FormatException("최상위가 객체가 아닙니다.");
            }

            // timezone은 아직 Asia/Seoul만 쓰므로 읽지 않고 KST(UTC+9) 고정이다.
            var set = new GachaBannerSet
            {
                DailyResetHour = (int)Number(root, "dailyResetHour", 5.0),
            };
            if (root.TryGetValue("rotation", out var rotationValue) && rotationValue is Dictionary<string, object> rotation)
            {
                set.RotationEnabled = rotation.TryGetValue("enabled", out var enabled) && enabled is bool on && on;
                set.RotationActiveCount = (int)Number(rotation, "activeCount");
                set.RotationPeriodDays = (int)Number(rotation, "periodDays");
            }

            var defaultCounts = Counts(root, "pullCounts", new[] { 1, GachaService.TenPull });
            foreach (var entry in List(root, "banners"))
            {
                var map = entry as Dictionary<string, object>;
                var banner = map != null ? Banner(map, catalog, defaultCounts) : null;
                if (banner != null)
                {
                    set.Banners.Add(banner);
                }
            }

            set.Banners.Sort((a, b) => a.Order.CompareTo(b.Order));
            return set;
        }

        static GachaBanner Banner(Dictionary<string, object> map, GachaCatalog catalog, int[] defaultCounts)
        {
            var id = Text(map, "id");
            if (!TryType(Text(map, "type"), out var type))
            {
                Debug.LogWarning("알 수 없는 배너 종류: " + Text(map, "type") + " (" + id + ")");
                return null;
            }

            var banner = new GachaBanner
            {
                Id = id,
                Type = type,
                Order = (int)Number(map, "order"),
                NameKo = Text(map, "nameKo"),
                NameEn = Text(map, "nameEn"),
                DescKo = Text(map, "descKo"),
                Price = (int)Number(map, "price"),
                PullCounts = Counts(map, "pullCounts", defaultCounts),
                BannerImage = Text(map, "bannerImage"),
            };

            if (type == BannerType.Standard || type == BannerType.Pickup)
            {
                banner.Machine = catalog.FindMachine("machine_" + Text(map, "machineId"));
                if (banner.Machine == null)
                {
                    Debug.LogWarning("배너 머신을 찾지 못했습니다: " + Text(map, "machineId") + " (" + id + ")");
                    return null;
                }

                // 상시 머신은 가격·확률을 머신 그대로 쓴다.
                if (type == BannerType.Standard)
                {
                    banner.Price = banner.Machine.Price;
                }
            }

            switch (type)
            {
                case BannerType.Pickup:
                    if (!ReadPickup(map, banner, catalog))
                    {
                        return null;
                    }

                    break;
                case BannerType.Festival:
                    ReadFestival(map, banner);
                    break;
                case BannerType.Daily:
                    ReadDaily(map, banner, catalog);
                    break;
            }

            return banner;
        }

        static bool ReadPickup(Dictionary<string, object> map, GachaBanner banner, GachaCatalog catalog)
        {
            banner.Featured = catalog.FindGear(Text(map, "featuredRewardId"));
            if (banner.Featured == null || catalog.MachineOf(banner.Featured.Id) != banner.Machine)
            {
                Debug.LogWarning("픽업 장비를 머신에서 찾지 못했습니다: " + Text(map, "featuredRewardId") + " (" + banner.Id + ")");
                return false;
            }

            var rule = map.TryGetValue("pickupRule", out var ruleValue) ? ruleValue as Dictionary<string, object> : null;
            var kind = rule != null ? Text(rule, "kind") : string.Empty;
            if (kind == "synchro")
            {
                banner.Pickup = PickupKind.Synchro;
                var rates = Rates(rule, "gradeRates");
                banner.LegendRate = rates != null ? rates[(int)ItemRarity.Legend] : -1f;
                banner.SynchroRate = rates != null ? rates[(int)ItemRarity.Synchro] : -1f;
            }
            else
            {
                banner.Pickup = PickupKind.Legend;
                banner.FeaturedShare = rule != null ? Mathf.Clamp01((float)Number(rule, "featuredShareOfGrade", 1.0)) : 1f;
            }

            return true;
        }

        static void ReadFestival(Dictionary<string, object> map, GachaBanner banner)
        {
            banner.GradeRates = Rates(map, "gradeRates");
            if (map.TryGetValue("limit", out var limitValue) && limitValue is Dictionary<string, object> limit)
            {
                banner.MaxPulls = (int)Number(limit, "maxPulls");
            }

            if (map.TryGetValue("pity", out var pityValue) && pityValue is Dictionary<string, object> pity)
            {
                banner.PityAt = (int)Number(pity, "atPull");
                if (TryRarity(Text(pity, "minGrade"), out var min))
                {
                    banner.PityMin = min;
                }
            }
        }

        static void ReadDaily(Dictionary<string, object> map, GachaBanner banner, GachaCatalog catalog)
        {
            if (TryRarity(Text(map, "guaranteeMinGrade"), out var min))
            {
                banner.GuaranteeMin = min;
            }

            if (map.TryGetValue("limit", out var limitValue) && limitValue is Dictionary<string, object> limit)
            {
                banner.MaxPulls = (int)Number(limit, "maxPulls", 1.0);
            }

            if (!(map.TryGetValue("weekdayPool", out var poolValue) && poolValue is Dictionary<string, object> pool))
            {
                return;
            }

            for (var day = 0; day < Weekdays.Length; day++)
            {
                var key = Text(pool, Weekdays[day]);
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                var machine = key == "all" ? null : catalog.FindMachine("machine_" + key);
                if (key != "all" && machine == null)
                {
                    Debug.LogWarning("데일리 요일 머신을 찾지 못했습니다: " + key);
                    continue;
                }

                banner.WeekdayPool[(DayOfWeek)day] = machine;
            }
        }

        static float[] Rates(Dictionary<string, object> map, string key)
        {
            if (!(map.TryGetValue(key, out var value) && value is Dictionary<string, object> grades))
            {
                return null;
            }

            var rates = new float[GachaService.RarityCount];
            foreach (var pair in grades)
            {
                if (TryRarity(pair.Key, out var rarity) && pair.Value is double rate)
                {
                    rates[(int)rarity] = (float)rate;
                }
            }

            return rates;
        }

        static int[] Counts(Dictionary<string, object> map, string key, int[] fallback)
        {
            if (!(map.TryGetValue(key, out var value) && value is List<object> list))
            {
                return fallback;
            }

            var counts = new List<int>();
            foreach (var item in list)
            {
                if (item is double number && number >= 1.0)
                {
                    counts.Add((int)number);
                }
            }

            return counts.Count > 0 ? counts.ToArray() : fallback;
        }

        static bool TryType(string id, out BannerType type)
        {
            switch (id)
            {
                case "standard": type = BannerType.Standard; return true;
                case "pickup": type = BannerType.Pickup; return true;
                case "daily": type = BannerType.Daily; return true;
                case "festival": type = BannerType.Festival; return true;
                default: type = default; return false;
            }
        }

        static bool TryRarity(string id, out ItemRarity rarity)
        {
            return Enum.TryParse(id, out rarity) && Enum.IsDefined(typeof(ItemRarity), rarity);
        }

        static IEnumerable<object> List(Dictionary<string, object> map, string key)
        {
            return map.TryGetValue(key, out var value) && value is List<object> list ? list : (IEnumerable<object>)Array.Empty<object>();
        }

        static string Text(Dictionary<string, object> map, string key)
        {
            return map.TryGetValue(key, out var value) && value is string text ? text : string.Empty;
        }

        static double Number(Dictionary<string, object> map, string key, double fallback = 0.0)
        {
            return map.TryGetValue(key, out var value) && value is double number ? number : fallback;
        }
    }
}
