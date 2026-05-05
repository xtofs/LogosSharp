using System.Numerics;
// ASCII characters in category Po: ['!"', '#', '$', '%', '&', "'", '*', '+', ',', '-', '.', '/', ':', ';', '?', '@', '^', '~']


public static class UInt128Extensions
{
    extension(UInt128 value)
    {
        public int TrailingZeroCount()
        {
            ulong lower = (ulong)value;
            ulong upper = (ulong)(value >> 64);

            if (lower != 0)
            {
                return BitOperations.TrailingZeroCount(lower);
            }
            else
            {
                return 64 + BitOperations.TrailingZeroCount(upper);
            }
        }

        public int PopCount()
        {
            ulong lower = (ulong)value;
            ulong upper = (ulong)(value >> 64);

            return BitOperations.PopCount(lower) + BitOperations.PopCount(upper);
        }

        public IEnumerable<int> EnumerateBits()
        {
            while (value != UInt128.Zero)
            {
                yield return value.TrailingZeroCount(); // Get the index of the least significant bit

                value &= value - 1; // Clear the least significant bit
            }
        }
    }


}
