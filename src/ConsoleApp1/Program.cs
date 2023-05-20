using System.Text.Encodings.Web;
using System.Text.Json;

var json = """
    {
        "Text" : "QQQQ\uD83D\uD83D你好\uD83D\uDE00\uDE00ZZZZZ\uD83D\uDE00\uD83D\uDE00"
    }
    """;

var options3 = new JsonSerializerOptions
{
    
    WriteIndented = true
};

var content = JsonSerializer.Deserialize<Content>(json, options3);

var text = JsonSerializer.Serialize(content);

Console.WriteLine(text);
