using System;

namespace ScrapFishing.Gacha
{
    public enum GearSlot
    {
        Lure,
        Boat,
        Drone,
        Suit
    }

    public enum GearState
    {
        Unprocessed,
        Stored,
        Equipped
    }

    // 수치 단위: Capacity는 개수, Seconds는 초, Shield는 횟수, 나머지는 % 값이다.
    public enum GearStat
    {
        LureCapacity,
        CatchRadiusPercent,
        RareChancePercent,
        SellPercent,
        GaugeSpeedPercent,
        EndBonusPercent,
        DroneRadiusPercent,
        HazardWarning,
        AutoCollectSeconds,
        HazardShield,
        OxygenPercent,
        HazardDamageReductionPercent
    }

    [Serializable]
    public struct GearEffect
    {
        public GearStat stat;
        public float value;

        public GearEffect(GearStat stat, float value)
        {
            this.stat = stat;
            this.value = value;
        }
    }
}
