using System.Reflection;
using Microsoft.Extensions.Logging;
using Winterflood.RuleEngine.Constants;

namespace Winterflood.RuleEngine.Compiler.Runners;

/// <summary>
/// Resolves a compiled RuleSet from a dynamically loaded assembly using reflection.
/// </summary>
public static class CompiledRuleSetResolver
{
    /// <summary>
    /// Attempts to resolve and instantiate a compiled RuleSet by name from the provided assembly.
    /// </summary>
    /// <param name="assembly">
    /// The assembly containing the compiled RuleSet classes.
    /// </param>
    /// <param name="ruleSetName">
    /// The name of the RuleSet to resolve. This must match the class name generated during compilation.
    /// </param>
    /// <param name="logger">
    /// An <see cref="ILogger"/> instance used to log any errors or warnings.
    /// </param>
    /// <param name="loggerFactory">
    /// A logger factory passed to the static <c>Create</c> method of the RuleSet for logger injection.
    /// </param>
    /// <returns>
    /// An instance of the resolved RuleSet if successful; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// This method expects the compiled RuleSet class to follow the convention:
    /// <c>{CompilerArtifactConstants.CompilerGenerated}.{RuleSetName}</c> and expose a static method named <c>Create</c>
    /// accepting a <see cref="ILoggerFactory"/> as parameter.
    /// </remarks>
    public static object? TryResolveRuleSet(
        Assembly assembly,
        string ruleSetName,
        ILogger logger,
        ILoggerFactory loggerFactory)
    {
        var type = assembly.GetType($"{CompilerArtifactConstants.CompilerGenerated}.{ruleSetName}");
        if (type == null)
        {
            logger.LogError("RuleSet Type not found for RuleSet={RuleSetName}", ruleSetName);
            return null;
        }

        var createMethod = type.GetMethod("Create");
        if (createMethod == null)
        {
            logger.LogError("Missing Create method for RuleSet={RuleSetName}", ruleSetName);
            return null;
        }

        try
        {
            return createMethod.Invoke(null, [loggerFactory]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error invoking Create for RuleSet={RuleSetName}", ruleSetName);
            return null;
        }
    }
}
