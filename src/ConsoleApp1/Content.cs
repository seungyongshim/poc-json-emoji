using System.Text.Json.Serialization;

public record Content
{
    [JsonConverter(typeof(JsonStringConveter))]
    public required string Text { get; init; }
}
