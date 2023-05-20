using System.Runtime.CompilerServices;
using System.Text.Json;

public static partial class NonExceptionHelper
{
    private class JsonHelpers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInRangeInclusive(uint value, uint lowerBound, uint upperBound)
           => (value - lowerBound) <= (upperBound - lowerBound);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInRangeInclusive(int value, int lowerBound, int upperBound)
            => (uint)(value - lowerBound) <= (uint)(upperBound - lowerBound);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInRangeInclusive(long value, long lowerBound, long upperBound)
            => (ulong)(value - lowerBound) <= (ulong)(upperBound - lowerBound);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInRangeInclusive(JsonTokenType value, JsonTokenType lowerBound, JsonTokenType upperBound)
            => (value - lowerBound) <= (upperBound - lowerBound);

    }
}
