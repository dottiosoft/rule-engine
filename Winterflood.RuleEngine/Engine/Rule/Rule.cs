using Winterflood.RuleEngine.Engine.Context;
using Winterflood.RuleEngine.Engine.Data;

namespace Winterflood.RuleEngine.Engine.Rule;

/// <summary>
/// Represents a configurable rule that evaluates an input data object (`TRuleData`)
/// and updates data object based on the evaluation result.
/// </summary>
/// <typeparam name="TRuleData">The input data type that the rule operates on.</typeparam>
public class Rule<TRuleData> : IRule<TRuleData>
    where TRuleData : class, IRuleData
{
    /// <summary>
    /// The unique name of the rule.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The condition that determines if the rule should execute successfully.
    /// Defaults to always true if not provided.
    /// </summary>
    public Func<TRuleData, RootContext, bool> Condition { get; }

    /// <summary>
    /// Defines the logic to execute when the rule evaluation is successful.
    /// </summary>
    public Action<TRuleData, RootContext> OnSuccess { get; }

    /// <summary>
    /// Defines the logic to execute when the rule evaluation fails.
    /// If not provided, the default failure message is returned.
    /// </summary>
    public Action<TRuleData, RootContext>? OnFailure { get; }

    /// <summary>
    /// Creates a rule that always executes successfully.
    /// </summary>
    /// <param name="name">Unique name of the rule.</param>
    /// <param name="onSuccess">Action to perform when the rule passes.</param>
    public Rule(
        string name,
        Action<TRuleData, RootContext> onSuccess)
    {
        Name = name;
        Condition = (_, _) => true;
        OnSuccess = onSuccess;
    }

    /// <summary>
    /// Creates a rule with a condition that determines if it executes successfully.
    /// </summary>
    /// <param name="name">Unique name of the rule.</param>
    /// <param name="condition">Condition function that determines success or failure.</param>
    /// <param name="onSuccess">Action to perform when the rule passes.</param>
    public Rule(
        string name,
        Func<TRuleData, RootContext, bool> condition,
        Action<TRuleData, RootContext> onSuccess)
    {
        Name = name;
        Condition = condition;
        OnSuccess = onSuccess;
    }

    /// <summary>
    /// Creates a rule with a condition, a success action, and a failure action.
    /// </summary>
    /// <param name="name">Unique name of the rule.</param>
    /// <param name="condition">Condition function that determines success or failure.</param>
    /// <param name="onSuccess">Action to perform when the rule passes.</param>
    /// <param name="onFailure">Action to perform when the rule fails.</param>
    public Rule(
        string name,
        Func<TRuleData, RootContext, bool> condition,
        Action<TRuleData, RootContext> onSuccess,
        Action<TRuleData, RootContext> onFailure)
    {
        Name = name;
        Condition = condition;
        OnSuccess = onSuccess;
        OnFailure = onFailure;
    }

    /// <summary>
    /// Evaluates the rule based on the input data and rule condition.
    /// </summary>
    /// <param name="data">The input data to evaluate.</param>
    /// <param name="rootContext">The ruleset context used for execution tracking.</param>
    /// <returns>Returns true if the rule passes, otherwise false.</returns>
    public bool Evaluate(TRuleData data, RootContext rootContext)
        => Condition(data, rootContext);

    /// <summary>
    /// Executes the success action when the rule passes.
    /// </summary>
    /// <param name="data">The input data.</param>
    /// <param name="rootContext">The ruleset context.</param>
    /// <returns>Returns the result of the success action.</returns>
    public object Success(TRuleData data, RootContext rootContext)
    {
        OnSuccess(data, rootContext);
        return true;
    }

    /// <summary>
    /// Executes the failure action when the rule fails.
    /// If no failure action is provided, a default failure message is returned.
    /// </summary>
    /// <param name="data">The input data.</param>
    /// <param name="rootContext">The ruleset context.</param>
    /// <returns>Returns the result of the failure action.</returns>
    public object Failure(TRuleData data, RootContext rootContext)
    {
        OnFailure?.Invoke(data, rootContext);
        return false;
    }
}
