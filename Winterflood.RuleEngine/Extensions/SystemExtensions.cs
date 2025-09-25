using System.Text.Json;

namespace Winterflood.RuleEngine.Extensions;

public static class SystemExtension
{
    public static T? Clone<T>(this T source) where T : class
    {
        var serialized = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<T>(serialized);
    }
}