using System;

namespace ScrapFishing.Gacha
{
    public interface IGachaRandom
    {
        // [0, 1) 범위의 값.
        float Next();
    }

    public sealed class SystemGachaRandom : IGachaRandom
    {
        readonly Random _random;

        public SystemGachaRandom()
            : this(Environment.TickCount)
        {
        }

        public SystemGachaRandom(int seed)
        {
            _random = new Random(seed);
        }

        public float Next()
        {
            // double → float 변환에서 1에 아주 가까운 값이 1f로 반올림되지 않게 막는다.
            return Math.Min((float)_random.NextDouble(), 0.99999994f);
        }
    }
}
