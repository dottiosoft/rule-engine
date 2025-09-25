using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Winterflood.RuleEngine.Constants;

namespace Winterflood.RuleEngine.Compiler.Runners;

/// <summary>
/// Provides utility methods for resolving and deserializing compiled rule engine data types.
/// </summary>
public static class CompiledDataResolver
{
    /// <summary>
    /// Attempts to resolve a type from the compiled assembly and deserialize the JSON input to that type.
    /// </summary>
    /// <param name="assembly">The compiled assembly containing generated types.</param>
    /// <param name="dataType">The name of the data type (without namespace prefix).</param>
    /// <param name="jsonData">The JSON string to deserialize.</param>
    /// <param name="logger">Logger for structured diagnostics.</param>
    /// <param name="result">The deserialized result if successful; otherwise null.</param>
    /// <returns>True if deserialization succeeded, otherwise false.</returns>
    public static bool TryResolveDataObject(
        Assembly assembly,
        string dataType,
        string? jsonData,
        ILogger logger,
        out object? result)
    {
        result = null;

        var fullTypeName = $"{CompilerArtifactConstants.CompilerGenerated}.{dataType}";
        var dataTypeRef = assembly.GetType(fullTypeName);

        if (dataTypeRef == null)
        {
            logger.LogError("DataType not found: {FullTypeName}", fullTypeName);
            return false;
        }

        try
        {
            result = JsonSerializer.Deserialize(jsonData ?? "{}", dataTypeRef, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            });

            if (result == null)
            {
                logger.LogError("Failed to deserialize JSON: DataType={DataType}, JSON={JsonData}", dataType, jsonData);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "JSON Deserialization failed for DataType={DataType}", dataType);
            return false;
        }
    }
}
