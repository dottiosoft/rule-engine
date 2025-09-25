using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Winterflood.RuleEngine.Compiler.Configuration.Models;

namespace Winterflood.RuleEngine.Compiler.Configuration;

/// <summary>
/// Provides functionality to parse rule engine configurations from JSON.
/// </summary>
public static class RuleDefinitionParser
{
    /// <summary>
    /// Parses a JSON string into a RuleEngineConfiguration object.
    /// </summary>
    /// <param name="json">The JSON string containing the rule engine configuration.</param>
    /// <param name="loggerFactory">Factory for structured logging.</param>
    /// <returns>A deserialized RuleEngineConfiguration object, or null if parsing fails.</returns>
    public static RuleEngineConfiguration? ParseConfiguration(string json, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(RuleDefinitionParser));

        if (string.IsNullOrWhiteSpace(json))
        {
            logger.LogError("Parsing failed: Input JSON is empty or null.");
            return null;
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            Converters = { new JsonRuleDefinitionConverter(loggerFactory), new JsonStringEnumConverter() }
        };

        try
        {
            logger.LogInformation("Parsing RuleEngineConfiguration from JSON.");
            var configuration = JsonSerializer.Deserialize<RuleEngineConfiguration>(json, options);

            if (configuration == null)
            {
                logger.LogError("Parsing failed: Deserialized configuration is null.");
                return null;
            }

            logger.LogInformation("Successfully parsed RuleEngineConfiguration.");
            return configuration;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "JSON Deserialization Error: Failed to parse RuleEngineConfiguration.");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during RuleEngineConfiguration parsing.");
            return null;
        }
    }
}
