using System;
using System.Runtime.CompilerServices;

namespace TryGet
{
    /// <summary>
    /// 固定容量 256 位的位数组（4 × ulong）。用于 Entity 的 Aspect/Tag 类型索引加速（V0.3 起）。
    ///
    /// 设计哲学（hsenl BitArray 启发，但容量扩到 256 以匹配 design.md 性能定位 &lt; 256 类型上限）：
    /// - struct 值类型，零分配
    /// - Add/Remove/Contains 全部位运算 O(1)
    /// - ContainsAll/ContainsAny 4 次 ulong 比较 O(1)
    /// - 256 位足够标识 Aspect + Tag 各 128 种（实际可按需共享或拆分）
    /// </summary>
    public struct BitArray256 : IEquatable<BitArray256>
    {
        /// <summary>容量上限（256 位）。</summary>
        public const int Capacity = 256;

        private ulong _l0;
        private ulong _l1;
        private ulong _l2;
        private ulong _l3;

        /// <summary>是否为空（无任何位被设置）。</summary>
        public bool IsEmpty => (_l0 | _l1 | _l2 | _l3) == 0UL;

        /// <summary>
        /// 设置位 <paramref name="index"/>（0..255）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int index)
        {
            ThrowIfOutOfRange(index);
            int bucket = index >> 6;          // / 64
            int bit = index & 0x3F;           // % 64
            ulong mask = 1UL << bit;
            switch (bucket)
            {
                case 0: _l0 |= mask; break;
                case 1: _l1 |= mask; break;
                case 2: _l2 |= mask; break;
                case 3: _l3 |= mask; break;
            }
        }

        /// <summary>
        /// 清除位 <paramref name="index"/>（0..255）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int index)
        {
            ThrowIfOutOfRange(index);
            int bucket = index >> 6;
            int bit = index & 0x3F;
            ulong mask = ~(1UL << bit);
            switch (bucket)
            {
                case 0: _l0 &= mask; break;
                case 1: _l1 &= mask; break;
                case 2: _l2 &= mask; break;
                case 3: _l3 &= mask; break;
            }
        }

        /// <summary>
        /// 检查位 <paramref name="index"/> 是否被设置。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(int index)
        {
            ThrowIfOutOfRange(index);
            int bucket = index >> 6;
            int bit = index & 0x3F;
            ulong mask = 1UL << bit;
            switch (bucket)
            {
                case 0: return (_l0 & mask) != 0;
                case 1: return (_l1 & mask) != 0;
                case 2: return (_l2 & mask) != 0;
                case 3: return (_l3 & mask) != 0;
            }
            return false;
        }

        /// <summary>
        /// 是否包含 <paramref name="other"/> 的全部位（this 是 other 的超集）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAll(in BitArray256 other)
        {
            return (_l0 & other._l0) == other._l0
                && (_l1 & other._l1) == other._l1
                && (_l2 & other._l2) == other._l2
                && (_l3 & other._l3) == other._l3;
        }

        /// <summary>
        /// 是否包含 <paramref name="other"/> 的任意一位（交集非空）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAny(in BitArray256 other)
        {
            return ((_l0 & other._l0) | (_l1 & other._l1) | (_l2 & other._l2) | (_l3 & other._l3)) != 0;
        }

        /// <summary>
        /// 清空所有位。
        /// </summary>
        public void Clear()
        {
            _l0 = _l1 = _l2 = _l3 = 0;
        }

        /// <summary>
        /// 统计已设置的位数（用于诊断）。
        /// </summary>
        public int PopCount()
        {
            return PopCountULong(_l0) + PopCountULong(_l1) + PopCountULong(_l2) + PopCountULong(_l3);
        }

        public bool Equals(BitArray256 other) =>
            _l0 == other._l0 && _l1 == other._l1 && _l2 == other._l2 && _l3 == other._l3;

        public override bool Equals(object obj) => obj is BitArray256 other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)(_l0 ^ (_l0 >> 32));
                hash = (hash * 397) ^ (int)(_l1 ^ (_l1 >> 32));
                hash = (hash * 397) ^ (int)(_l2 ^ (_l2 >> 32));
                hash = (hash * 397) ^ (int)(_l3 ^ (_l3 >> 32));
                return hash;
            }
        }

        public static bool operator ==(BitArray256 a, BitArray256 b) => a.Equals(b);
        public static bool operator !=(BitArray256 a, BitArray256 b) => !a.Equals(b);

        public override string ToString() => $"BitArray256(popCount={PopCount()})";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfOutOfRange(int index)
        {
            if ((uint)index >= (uint)Capacity)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} out of range [0, {Capacity}).");
        }

        private static int PopCountULong(ulong v)
        {
            // SWAR popcount（避免依赖 BitOperations，保持 netstandard2.1 兼容）
            v = v - ((v >> 1) & 0x5555555555555555UL);
            v = (v & 0x3333333333333333UL) + ((v >> 2) & 0x3333333333333333UL);
            v = (v + (v >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)((v * 0x0101010101010101UL) >> 56);
        }
    }
}
