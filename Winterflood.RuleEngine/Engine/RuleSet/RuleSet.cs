using Microsoft.Extensions.Logging;
using Winterflood.RuleEngine.Engine.Context;
using Winterflood.RuleEngine.Engine.Data;
using Winterflood.RuleEngine.Engine.Rule;
using Winterflood.RuleEngine.Extensions;

namespace Winterflood.RuleEngine.Engine.RuleSet;

/// <summary>
/// Represents a collection of rules that operate on an input data object (`TData`).
/// Supports execution modes and nested rulesets.
/// </summary>
/// <typeparam name="TData">The input data type that the ruleset operates on.</typeparam>
public class RuleSet<TData>(
    string name,
    RuleExecutionMode ruleExecutionMode,
    ILoggerFactory loggerFactory) : IRuleSet<TData>
    where TData : class, IRuleData, new()
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<RuleSet<TData>>();

    /// <summary>
    /// List of rules within this ruleset.
    /// </summary>
    private readonly List<Lazy<IRule<TData>>> _rules = [];

    /// <summary>
    /// Adds a rule to the ruleset.
    /// </summary>
    /// <param name="ruleFactory">A factory function that creates a rule.</param>
    public void AddRule(Func<IRule<TData>> ruleFactory)
    {
        _logger.LogInformation("Adding to RuleSet={RuleSetName}", Name);
        _rules.Add(new Lazy<IRule<TData>>(ruleFactory));
    }

    /// <summary>
    /// The name of the ruleset.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Evaluates the ruleset by executing its rules in sequence.
    /// </summary>
    /// <param name="data">The input data.</param>
    /// <param name="rootContext">The ruleset context for tracking execution.</param>
    /// <returns>Returns true if all applicable rules pass, otherwise false.</returns>
    public bool Evaluate(TData data, RootContext rootContext)
    {
        _logger.LogInformation(
            "[Evaluating] RuleSet={RuleSetName} Mode={RuleExecutionMode}",
            Name,
            ruleExecutionMode);

        foreach (var lazyRule in _rules)
        {
            var rule = lazyRule.Value;

            var ctx = new RuleContext
            {
                RuleName = rule.Name,
                RuleDataBeforeEvaluation = data.Clone(),
                Result = false
            };

            rootContext.SetChildContext(rule.Name, ctx);

            _logger.LogInformation(
                "[Evaluating] Rule={RuleName} for RuleSet={RuleSetName}",
                rule.Name,
                Name);

            if (rule.Evaluate(data, rootContext))
            {
                ctx.Result = true;
                ctx.Output = rule.Success(data, rootContext);
                ctx.RuleDataAfterEvaluation = data.Clone();

                _logger.LogInformation(
                    "[Rule Passed] Rule={RuleName} Output={RuleOutput} for RuleSet={RuleSetName}",
                    rule.Name,
                    ctx.Output,
                    Name);

                if (ruleExecutionMode != RuleExecutionMode.StopOnFirstSuccess)
                    continue;

                _logger.LogInformation("Short-circuiting evaluation for RuleSet={RuleSetName} for first successful result", Name);
                return true;
            }

            ctx.Result = false;
            ctx.Output = rule.Failure(data, rootContext);
            ctx.RuleDataAfterEvaluation = data.Clone();

            _logger.LogInformation(
                "[Rule Failed] Rule={RuleName} Output={RuleOutput} for RuleSet={RuleSetName}",
                rule.Name,
                ctx.Output,
                Name);

            if (ruleExecutionMode != RuleExecutionMode.StopOnFirstFailure)
                continue;

            _logger.LogInformation("Short-circuiting evaluation for RuleSet={RuleSetName} for first failed result", Name);
            return false;
        }

        _logger.LogInformation("Completed evaluating RuleSet={RuleSetName}", Name);

        return VerifyAllChildRuleContexts(rootContext);
    }

    private static bool VerifyAllChildRuleContexts(RootContext rootContext)
    {
        return rootContext.ChildContexts.All(x =>
        {
            return x.Value switch
            {
                RuleContext ruleContext => ruleContext.Result,
                // Recurse....
                RootContext nestedContext => VerifyAllChildRuleContexts(nestedContext),
                _ => true
            };
        });
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="data"></param>
    /// <param name="rootContext"></param>
    /// <returns></returns>
    public object Success(TData data, RootContext rootContext) => true;
    /// <summary>
    /// 
    /// </summary>
    /// <param name="data"></param>
    /// <param name="rootContext"></param>
    /// <returns></returns>
    public object Failure(TData data, RootContext rootContext) => false;
}