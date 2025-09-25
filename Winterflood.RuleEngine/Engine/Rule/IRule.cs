using Winterflood.RuleEngine.Engine.Context;

namespace Winterflood.RuleEngine.Engine.Rule;

/// <summary>
/// Defines a rule that can be evaluated based on input data and modifies output data accordingly.
/// </summary>
/// <typeparam name="TData">The input data type that the rule operates on.</typeparam>
public interface IRule<in TData> 
    where TData : class
{
    /// <summary>
    /// The unique name of the rule.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Evaluates the rule based on the provided input data and updates the output data accordingly.
    /// </summary>
    /// <param name="input">The input data to evaluate.</param>
    /// <param name="rootContext">The ruleset context used to store rule execution details.</param>
    /// <returns>
    /// Returns <c>true</c> if the rule passes evaluation; otherwise, <c>false</c>.
    /// </returns>
    bool Evaluate(TData input, RootContext rootContext);

    /// <summary>
    /// Defines the behavior when the rule evaluation succeeds.
    /// This method can modify the output object or perform additional actions.
    /// </summary>
    /// <param name="input">The input data that was evaluated.</param>
    /// <param name="rootContext">The ruleset context used to store rule execution details.</param>
    /// <returns>
    /// Returns an object representing the success result.
    /// </returns>
    object Success(TData input, RootContext rootContext);

    /// <summary>
    /// Defines the behavior when the rule evaluation fails.
    /// This method can modify the output object or perform additional actions.
    /// </summary>
    /// <param name="input">The input data that was evaluated.</param>
    /// <param name="rootContext">The ruleset context used to store rule execution details.</param>
    /// <returns>
    /// Returns an object representing the failure result.
    /// </returns>
    object Failure(TData input, RootContext rootContext);
}
