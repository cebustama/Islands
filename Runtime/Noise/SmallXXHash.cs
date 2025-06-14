using System;
using Unity.Mathematics;

namespace Islands
{
    public readonly struct SmallXXHash
    {
        const uint primeA = 0b10011110001101110111100110110001;
        const uint primeB = 0b10000101111010111100101001110111;
        const uint primeC = 0b11000010101100101010111000111101;
        const uint primeD = 0b00100111110101001110101100101111;
        const uint primeE = 0b00010110010101100110011110110001;

        readonly uint accumulator;

        public SmallXXHash(uint accumulator)
        {
            // Only constructors are allowed to modify readonly fields
            this.accumulator = accumulator;
        }

        public static implicit operator SmallXXHash(uint accumulator) =>
            new SmallXXHash(accumulator);

        public static SmallXXHash Seed(int seed) => (uint)seed + primeE;

        // Bits that would be lost by a shift are reinserted on the other side by a rotation
        private static uint RotateLeft(uint data, int steps) =>
            (data << steps) | (data >> 32 - steps);

        public SmallXXHash Eat(int data) =>
            RotateLeft(accumulator + (uint)data * primeC, 17) * primeD;

        public SmallXXHash Eat(byte data) =>
            RotateLeft(accumulator + data * primeE, 11) * primeA;

        // This makes it so that we can directly assign a SmallXXHash value to a uint
        public static implicit operator uint(SmallXXHash hash)
        {
            uint avalanche = hash.accumulator;
            avalanche ^= avalanche >> 15;
            avalanche *= primeB;
            avalanche ^= avalanche >> 13;
            avalanche *= primeC;
            avalanche ^= avalanche >> 16;
            return avalanche;
        }

        public static implicit operator SmallXXHash4(SmallXXHash hash) =>
            new SmallXXHash4(hash.accumulator);
    }

    /// <summary>
    /// Operates on vectors of four values in parallel.
    /// </summary>
    public readonly struct SmallXXHash4
    {
        const uint primeB = 0b10000101111010111100101001110111;
        const uint primeC = 0b11000010101100101010111000111101;
        const uint primeD = 0b00100111110101001110101100101111;
        const uint primeE = 0b00010110010101100110011110110001;

        readonly uint4 accumulator;

        public uint4 BytesA => (uint4)this & 255;
        public uint4 BytesB => ((uint4)this >> 8) & 255;
        public uint4 BytesC => ((uint4)this >> 16) & 255;
        public uint4 BytesD => (uint4)this >> 24;

        public float4 Floats01A => (float4)BytesA * (1f / 255f);
        public float4 Floats01B => (float4)BytesB * (1f / 255f);
        public float4 Floats01C => (float4)BytesC * (1f / 255f);
        public float4 Floats01D => (float4)BytesD * (1f / 255f);

        public uint4 GetBits(int count, int shift) =>
            ((uint4)this >> shift) & (uint)((1 << count) - 1);

        public float4 GetBitsAsFloats01(int count, int shift) =>
            (float4)GetBits(count, shift) * (1f / ((1 << count) - 1));

        public SmallXXHash4(uint4 accumulator)
        {
            // Only constructors are allowed to modify readonly fields
            this.accumulator = accumulator;
        }

        public static implicit operator SmallXXHash4(uint4 accumulator) =>
            new SmallXXHash4(accumulator);

        public static SmallXXHash4 Seed(int4 seed) => (uint4)seed + primeE;

        // Bits that would be lost by a shift are reinserted on the other side by a rotation
        private static uint4 RotateLeft(uint4 data, int steps) =>
            (data << steps) | (data >> 32 - steps);

        public SmallXXHash4 Eat(int4 data) =>
            RotateLeft(accumulator + (uint4)data * primeC, 17) * primeD;

        // This makes it so that we can directly assign a SmallXXHash value to a uint
        public static implicit operator uint4(SmallXXHash4 hash)
        {
            uint4 avalanche = hash.accumulator;
            avalanche ^= avalanche >> 15;
            avalanche *= primeB;
            avalanche ^= avalanche >> 13;
            avalanche *= primeC;
            avalanche ^= avalanche >> 16;
            return avalanche;
        }

        public static SmallXXHash4 operator +(SmallXXHash4 h, int v) =>
            h.accumulator + (uint)v;

        public static SmallXXHash4 Select(SmallXXHash4 a, SmallXXHash4 b, bool4 c) =>
            math.select(a.accumulator, b.accumulator, c);
    }
}