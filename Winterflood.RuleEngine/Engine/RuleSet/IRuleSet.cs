using Winterflood.RuleEngine.Engine.Data;
using Winterflood.RuleEngine.Engine.Rule;

namespace Winterflood.RuleEngine.Engine.RuleSet;

/// <summary>
/// Represents a ruleset that evaluates a collection of rules.
/// Rulesets can be nested within other rulesets to allow for hierarchical rule execution.
/// </summary>
/// <typeparam name="TData">The input data type that the rules operate on.</typeparam>
public interface IRuleSet<TData> : IRule<TData> 
    where TData : class, IRuleData, new()
{
    /// <summary>
    /// Adds a new rule to the ruleset.
    /// </summary>
    /// <param name="ruleFactory">
    /// A factory function that creates an instance of an <see cref="IRule{TData}"/>.
    /// </param>
    void AddRule(Func<IRule<TData>> ruleFactory);
}
