using System;

namespace ScrapFishing.Gacha
{
    // 정수 값은 저장 데이터(장착 칸 순서)와 맞물려 있어서, 이름만 바꾸고 새 슬롯은 맨 뒤에 붙인다. 3번은 예전 Suit였다.
    public enum GearSlot
    {
        Lure,
        Boat,
        Drone,
        Helmet,
        Fisher,
        Rod
    }

    public enum GearState
    {
        Unprocessed,
        Stored,
        Equipped
    }

    // 수치 단위: Capacity·SpawnCapBonus는 개수, Seconds는 초, Shield는 횟수, DiveEveryCastsBonus는 캐스트 수,
    // RareChancePercent는 %p, 나머지는 % 값이다. 저장값과 순서를 맞추려고 새 항목은 맨 뒤에만 붙인다.
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
        HazardDamageReductionPercent,
        SpawnCapBonus,
        SteerSpeedPercent,
        ReelSpeedPercent,
        DiveSpeedPercent,
        DiveEveryCastsBonus,
        HookHitRadiusPercent,
        MaxDepthPercent,
        BonusCatchChancePercent
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
