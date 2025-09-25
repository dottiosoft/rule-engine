using System.Reflection;
using Microsoft.Extensions.Logging;
using Winterflood.RuleEngine.Engine.Context;

namespace Winterflood.RuleEngine.Compiler.Runners;

/// <summary>
/// Provides utility methods to resolve and invoke the <c>Evaluate</c> method on a compiled RuleSet instance.
/// </summary>
public static class EvaluationExecutor
{
    /// <summary>
    /// Resolves the Evaluate method and invokes it with the provided input and context.
    /// </summary>
    /// <param name="ruleSetInstance">The compiled rule set instance.</param>
    /// <param name="input">The input object to evaluate.</param>
    /// <param name="context">The execution context.</param>
    /// <param name="logger">The logger for diagnostics.</param>
    /// <param name="ruleSetName">The name of the rule set (for logging).</param>
    /// <returns>True if evaluation succeeds and returns true; otherwise false.</returns>
    public static bool TryEvaluate(object ruleSetInstance, object input, RootContext context, ILogger logger, string ruleSetName)
    {
        var method = ResolveEvaluateMethod(ruleSetInstance, logger, ruleSetName);
        return method is not null && TryInvokeEvaluate(method, ruleSetInstance, input, context, logger);
    }

    /// <summary>
    /// Attempts to resolve the <c>Evaluate</c> method from a compiled RuleSet instance.
    /// </summary>
    /// <param name="ruleSetInstance">The compiled RuleSet instance to inspect.</param>
    /// <param name="logger">Logger for reporting missing method issues.</param>
    /// <param name="ruleSetName">The name of the RuleSet (for logging purposes).</param>
    /// <returns>
    /// A <see cref="MethodInfo"/> representing the <c>Evaluate</c> method, or <c>null</c> if not found.
    /// </returns>
    /// <remarks>
    /// The method must be named <c>Evaluate</c> and is expected to accept two parameters:
    /// the rule input model and a <see cref="RootContext"/> instance.
    /// </remarks>
    public static MethodInfo? ResolveEvaluateMethod(object ruleSetInstance, ILogger logger, string ruleSetName)
    {
        var method = ruleSetInstance.GetType().GetMethod("Evaluate");
        if (method == null)
        {
            logger.LogError("'Evaluate' method not found: RuleSet={RuleSetName}", ruleSetName);
        }

        return method;
    }

    /// <summary>
    /// Invokes the <c>Evaluate</c> method on the given RuleSet instance with the specified input and context.
    /// </summary>
    /// <param name="method">The <see cref="MethodInfo"/> representing the Evaluate method.</param>
    /// <param name="instance">The instance of the RuleSet to invoke the method on.</param>
    /// <param name="input">The rule input data object.</param>
    /// <param name="context">The <see cref="RootContext"/> for evaluation tracking.</param>
    /// <param name="logger">Logger used to report invocation errors.</param>
    /// <returns><c>true</c> if the evaluation succeeded (returns true); otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This method safely handles invocation errors and logs them.
    /// </remarks>
    public static bool TryInvokeEvaluate(MethodInfo method, object instance, object input, RootContext context,
        ILogger logger)
    {
        try
        {
            method.Invoke(instance, [input, context]);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Evaluation failed");
            return false;
        }
    }
}