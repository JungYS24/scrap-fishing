using System.Collections.Generic;

namespace ScrapFishing.Fishing
{
    public sealed class LureLoad
    {
        public const int ChipToGoldRatio = 20;

        static readonly int[] DefaultCapacity = { 4, 6, 8, 10, 12 };

        readonly List<int> _attached = new List<int>();
        readonly HashSet<int> _cashed = new HashSet<int>();
        int _maxAttach;

        public LureLoad(int maxAttach)
        {
            _maxAttach = maxAttach < 0 ? 0 : maxAttach;
        }

        public int Count => _attached.Count;
        public int MaxAttach => _maxAttach;
        public bool IsFull => _attached.Count >= _maxAttach;

        public void SetMaxAttach(int maxAttach)
        {
            _maxAttach = maxAttach < 0 ? 0 : maxAttach;
        }

        public static bool IsHazard(int amount)
        {
            return amount < 0;
        }

        public static int GoldFromAmount(int amount)
        {
            return amount / ChipToGoldRatio;
        }

        public static int CapacityForLevel(int level, int[] table = null)
        {
            var source = table != null && table.Length > 0 ? table : DefaultCapacity;
            if (level < 0)
            {
                return source[0];
            }

            if (level >= source.Length)
            {
                return source[source.Length - 1];
            }

            return source[level];
        }

        public bool TryAttach(int id, int amount)
        {
            if (IsHazard(amount) || _attached.Contains(id) || IsFull)
            {
                return false;
            }

            _attached.Add(id);
            return true;
        }

        public bool Contains(int id)
        {
            return _attached.Contains(id);
        }

        public bool TryCash(int id)
        {
            if (!_attached.Contains(id) || _cashed.Contains(id))
            {
                return false;
            }

            _cashed.Add(id);
            return true;
        }

        public void Clear()
        {
            _attached.Clear();
            _cashed.Clear();
        }
    }
}
