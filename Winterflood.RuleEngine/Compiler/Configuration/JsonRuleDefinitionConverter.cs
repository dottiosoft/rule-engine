using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Winterflood.RuleEngine.Compiler.Configuration.Models;

namespace Winterflood.RuleEngine.Compiler.Configuration;

/// <summary>
/// JSON Converter for RuleDefinition that handles deserialization of different rule types.
/// </summary>
public class JsonRuleDefinitionConverter : JsonConverter<RuleDefinition>
{
    private readonly ILogger<JsonRuleDefinitionConverter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonRuleDefinitionConverter"/> class.
    /// </summary>
    /// <param name="loggerFactory">Logger factory for structured logging.</param>
    public JsonRuleDefinitionConverter(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<JsonRuleDefinitionConverter>();
    }

    /// <summary>
    /// Reads and deserializes a RuleDefinition from JSON.
    /// </summary>
    /// <param name="reader">The JSON reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">Serialization options.</param>
    /// <returns>A deserialized <see cref="RuleDefinition"/> object.</returns>
    /// <exception cref="JsonException">Thrown when required properties are missing or invalid.</exception>
    public override RuleDefinition Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        try
        {
            using var jsonDoc = JsonDocument.ParseValue(ref reader);
            var root = jsonDoc.RootElement;

            if (!root.TryGetProperty("type", out var typeProperty) || typeProperty.ValueKind != JsonValueKind.String)
            {
                _logger.LogError("JSON Deserialization Failed: Missing or invalid 'type' property.");
                throw new JsonException("Missing or invalid 'type' property in RuleDefinition.");
            }

            var ruleTypeStr = typeProperty.GetString();
            if (string.IsNullOrWhiteSpace(ruleTypeStr) || !Enum.TryParse(ruleTypeStr, true, out RuleType ruleType))
            {
                _logger.LogError("JSON Deserialization Failed: Invalid rule type value '{RuleType}'", ruleTypeStr);
                throw new JsonException($"Invalid rule type: '{ruleTypeStr}' in RuleDefinition.");
            }

            _logger.LogInformation("Deserializing RuleDefinition: Type={RuleType}", ruleType);

            RuleDefinition? ruleDefinition = ruleType switch
            {
                RuleType.StandardRule => Deserialize<StandardRuleDefinition>(root, options),
                RuleType.NestedRuleSet => Deserialize<NestedRuleDefinition>(root, options),
                _ => throw new NotImplementedException($"Unsupported rule type: {ruleType}")
            };

            if (ruleDefinition == null)
            {
                _logger.LogError("JSON Deserialization Failed: RuleDefinition could not be deserialized.");
                throw new JsonException($"Failed to deserialize RuleDefinition of type '{ruleType}'.");
            }

            _logger.LogInformation("Successfully Deserialized RuleDefinition: Type={RuleType}", ruleType);
            return ruleDefinition;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON Deserialization Error: RuleDefinition could not be processed.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected Error during RuleDefinition deserialization.");
            throw new JsonException("Unexpected error occurred while deserializing RuleDefinition.", ex);
        }
    }

    /// <summary>
    /// Writes a RuleDefinition to JSON.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The value to serialize.</param>
    /// <param name="options">Serialization options.</param>
    /// <exception cref="NotImplementedException">Thrown if an unsupported type is encountered.</exception>
    public override void Write(Utf8JsonWriter writer, RuleDefinition value, JsonSerializerOptions options)
    {
        try
        {
            switch (value)
            {
                case StandardRuleDefinition standardRule:
                    _logger.LogInformation("Serializing StandardRuleDefinition: RuleName={RuleName}", standardRule.RuleName);
                    JsonSerializer.Serialize(writer, standardRule, options);
                    break;

                case NestedRuleDefinition nestedRule:
                    _logger.LogInformation("Serializing NestedRuleSetRuleDefinition: RuleName={RuleName}", nestedRule.RuleName);
                    JsonSerializer.Serialize(writer, nestedRule, options);
                    break;

                default:
                    _logger.LogError("Serialization Failed: Unsupported rule type {RuleType}", value.GetType().Name);
                    throw new NotImplementedException($"Unsupported rule type: {value.GetType().Name}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while serializing RuleDefinition.");
            throw;
        }
    }

    /// <summary>
    /// Safely deserializes a JSON element into a specific RuleDefinition subclass.
    /// </summary>
    /// <typeparam name="T">The RuleDefinition subclass type.</typeparam>
    /// <param name="jsonElement">The JSON element containing rule data.</param>
    /// <param name="options">JSON serialization options.</param>
    /// <returns>The deserialized rule or null if an error occurs.</returns>
    private T? Deserialize<T>(JsonElement jsonElement, JsonSerializerOptions options) where T : RuleDefinition
    {
        try
        {
            var deserializedObject = JsonSerializer.Deserialize<T>(jsonElement.GetRawText(), options);
            if (deserializedObject == null)
            {
                _logger.LogError("JSON Deserialization Failed: RuleDefinition of type {RuleType} is null", typeof(T).Name);
            }

            return deserializedObject;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON Deserialization Error: RuleDefinition of type {RuleType}", typeof(T).Name);
            return null;
        }
    }
}
