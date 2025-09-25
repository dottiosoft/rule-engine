using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Winterflood.RuleEngine.Engine.Context;

namespace Winterflood.RuleEngine.Compiler.Runners;

/// <summary>
/// Executes a dynamically compiled ruleset by loading it from an assembly,
/// deserializing input data, and invoking the ruleset's evaluation logic.
/// </summary>
public static class RuleRunner
{
    /// <summary>
    /// Executes a compiled RuleSet by deserializing the input data and invoking the Evaluate method.
    /// </summary>
    public static void ExecuteRuleset(
        ILoggerFactory loggerFactory,
        Assembly assembly,
        string? ruleSetName,
        string? dataType,
        string? jsonData)
    {

        var logger = loggerFactory.CreateLogger(nameof(RuleRunner));

        if (string.IsNullOrWhiteSpace(ruleSetName) || string.IsNullOrWhiteSpace(dataType))
        {
            logger.LogError("Invalid input: RuleSetName={RuleSetName}, DataType={DataType}", ruleSetName, dataType);
            return;
        }

        logger.LogInformation("Executing RuleSet={RuleSetName}", ruleSetName);

        var ruleSetInstance =
            CompiledRuleSetResolver.TryResolveRuleSet(
                assembly,
                ruleSetName,
                logger,
                loggerFactory);

        if (ruleSetInstance is null)
            return;

        if (!CompiledDataResolver.TryResolveDataObject(assembly, dataType, jsonData, logger, out var data))
            return;

        var context = new RootContext();
        var success =
            EvaluationExecutor.TryEvaluate(
                ruleSetInstance,
                data!,
                context,
                logger,
                ruleSetName);

        logger.LogInformation(
            "RuleSet Execution Complete: RuleSetName={RuleSetName}, Result={Result}, Data={Data}",
            ruleSetName,
            success,
            JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }
}
