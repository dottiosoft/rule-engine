using System.Text.Json;
using System.Text.Json.Serialization;

namespace Winterflood.RuleEngine.Compiler.Configuration.Models;

/// <summary>
/// Represents a test case for a ruleset.
/// </summary>
public class RuleTestDefinition
{
    /// <summary>
    /// Gets or sets the input data for the test case.
    /// </summary>
    [JsonPropertyName("data")]
    public JsonElement Data { get; set; }

    /// <summary>
    /// Gets or sets the expected output for the test case.
    /// </summary>
    [JsonPropertyName("expect")]
    public string Expect { get; set; }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"TestCase: Data={Data}, Expected={Expect}";
    }
}