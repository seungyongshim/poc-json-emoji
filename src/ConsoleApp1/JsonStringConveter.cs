using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

public class JsonStringConveter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if(reader.ValueIsEscaped is true)
        {
            var span = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;

            return NonExceptionHelper.GetUnescapedString(span);
        }

        return reader.GetString();
    }
    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        // For performance, lift up the writer implementation.
        if (value == null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value.AsSpan());
        }
    }
}
