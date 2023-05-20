using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Text;

public static partial class NonExceptionHelper
{
    public static readonly UTF8Encoding s_utf8Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    public static string TranscodeHelper(ReadOnlySpan<byte> utf8Unescaped)
    {
        return s_utf8Encoding.GetString(utf8Unescaped);
    }

    public static string GetUnescapedString(ReadOnlySpan<byte> utf8Source)
    {
        // The escaped name is always >= than the unescaped, so it is safe to use escaped name for the buffer length.
        int length = utf8Source.Length;
        byte[]? pooledName = null;

        Span<byte> utf8Unescaped = length <= 256 ?
            stackalloc byte[256] :
            (pooledName = ArrayPool<byte>.Shared.Rent(length));

        Unescape(utf8Source, utf8Unescaped, out int written);

        utf8Unescaped = utf8Unescaped.Slice(0, written);

        string utf8String = TranscodeHelper(utf8Unescaped);

        if (pooledName != null)
        {
            utf8Unescaped.Clear();
            ArrayPool<byte>.Shared.Return(pooledName);
        }

        return utf8String;
    }

    public static void Unescape(ReadOnlySpan<byte> source, Span<byte> destination, out int written)
    {
        int idx = source.IndexOf(JsonConstants.BackSlash);
        bool result = TryUnescape(source, destination, idx, out written);
    }

    private static bool TryUnescape(ReadOnlySpan<byte> source, Span<byte> destination, int idx, out int written)
    {
        Debug.Assert(idx >= 0 && idx < source.Length);
        Debug.Assert(source[idx] == JsonConstants.BackSlash);

        if (!source.Slice(0, idx).TryCopyTo(destination))
        {
            written = 0;
            goto DestinationTooShort;
        }

        written = idx;

        while (true)
        {
            Debug.Assert(source[idx] == JsonConstants.BackSlash);

            if (written == destination.Length)
            {
                goto DestinationTooShort;
            }

            switch (source[++idx])
            {
                case JsonConstants.Quote:
                    destination[written++] = JsonConstants.Quote;
                    break;
                case (byte)'n':
                    destination[written++] = JsonConstants.LineFeed;
                    break;
                case (byte)'r':
                    destination[written++] = JsonConstants.CarriageReturn;
                    break;
                case JsonConstants.BackSlash:
                    destination[written++] = JsonConstants.BackSlash;
                    break;
                case JsonConstants.Slash:
                    destination[written++] = JsonConstants.Slash;
                    break;
                case (byte)'t':
                    destination[written++] = JsonConstants.Tab;
                    break;
                case (byte)'b':
                    destination[written++] = JsonConstants.BackSpace;
                    break;
                case (byte)'f':
                    destination[written++] = JsonConstants.FormFeed;
                    break;
                default:
                    Debug.Assert(source[idx] == 'u', "invalid escape sequences must have already been caught by Utf8JsonReader.Read()");

                    // The source is known to be valid JSON, and hence if we see a \u, it is guaranteed to have 4 hex digits following it
                    // Otherwise, the Utf8JsonReader would have already thrown an exception.
                    Debug.Assert(source.Length >= idx + 5);

                    bool result = Utf8Parser.TryParse(source.Slice(idx + 1, 4), out int scalar, out int bytesConsumed, 'x');
                    Debug.Assert(result);
                    Debug.Assert(bytesConsumed == 4);
                    idx += 4;

                    if (JsonHelpers.IsInRangeInclusive((uint)scalar, JsonConstants.HighSurrogateStartValue, JsonConstants.LowSurrogateEndValue))
                    {
                        // The first hex value cannot be a low surrogate.
                        if (scalar >= JsonConstants.LowSurrogateStartValue)
                        {
                            idx++;
                            goto Skip;
                        }

                        Debug.Assert(JsonHelpers.IsInRangeInclusive((uint)scalar, JsonConstants.HighSurrogateStartValue, JsonConstants.HighSurrogateEndValue));

                        // We must have a low surrogate following a high surrogate.
                        if (source.Length < idx + 7 || source[idx + 1] != '\\' || source[idx + 2] != 'u')
                        {
                            idx++;
                            goto Skip;
                        }

                        // The source is known to be valid JSON, and hence if we see a \u, it is guaranteed to have 4 hex digits following it
                        // Otherwise, the Utf8JsonReader would have already thrown an exception.
                        result = Utf8Parser.TryParse(source.Slice(idx + 3, 4), out int lowSurrogate, out bytesConsumed, 'x');
                        Debug.Assert(result);
                        Debug.Assert(bytesConsumed == 4);
                        idx += 6;

                        // If the first hex value is a high surrogate, the next one must be a low surrogate.
                        if (!JsonHelpers.IsInRangeInclusive((uint)lowSurrogate, JsonConstants.LowSurrogateStartValue, JsonConstants.LowSurrogateEndValue))
                        {
                            idx-=5;
                            goto Skip;
                            //ThrowHelper.ThrowInvalidOperationException_ReadInvalidUTF16(lowSurrogate);
                        }

                        // To find the unicode scalar:
                        // (0x400 * (High surrogate - 0xD800)) + Low surrogate - 0xDC00 + 0x10000
                        scalar = (JsonConstants.BitShiftBy10 * (scalar - JsonConstants.HighSurrogateStartValue))
                            + (lowSurrogate - JsonConstants.LowSurrogateStartValue)
                            + JsonConstants.UnicodePlane01StartValue;
                    }

                    var rune = new Rune(scalar);
                    bool success = rune.TryEncodeToUtf8(destination.Slice(written), out int bytesWritten);

                    if (!success)
                    {
                        goto DestinationTooShort;
                    }

                    written += bytesWritten;
                    break;
            }

            if (++idx == source.Length)
            {
                goto Success;
            }
        Skip:
            if (source[idx] != JsonConstants.BackSlash)
            {
                ReadOnlySpan<byte> remaining = source.Slice(idx);
                int nextUnescapedSegmentLength = remaining.IndexOf(JsonConstants.BackSlash);
                if (nextUnescapedSegmentLength < 0)
                {
                    nextUnescapedSegmentLength = remaining.Length;
                }

                if ((uint)(written + nextUnescapedSegmentLength) >= (uint)destination.Length)
                {
                    goto DestinationTooShort;
                }

                Debug.Assert(nextUnescapedSegmentLength > 0);
                switch (nextUnescapedSegmentLength)
                {
                    case 1:
                        destination[written++] = source[idx++];
                        break;
                    case 2:
                        destination[written++] = source[idx++];
                        destination[written++] = source[idx++];
                        break;
                    case 3:
                        destination[written++] = source[idx++];
                        destination[written++] = source[idx++];
                        destination[written++] = source[idx++];
                        break;
                    default:
                        remaining.Slice(0, nextUnescapedSegmentLength).CopyTo(destination.Slice(written));
                        written += nextUnescapedSegmentLength;
                        idx += nextUnescapedSegmentLength;
                        break;
                }

                Debug.Assert(idx == source.Length || source[idx] == JsonConstants.BackSlash);

                if (idx == source.Length)
                {
                    goto Success;
                }
            }
        }

        Success:
        return true;

        DestinationTooShort:
        return false;
    }
}
