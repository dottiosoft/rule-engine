using Winterflood.RuleEngine.Engine.Adapter;
using Winterflood.RuleEngine.Engine.Data;
using Winterflood.RuleEngine.Engine.Rule;
using Winterflood.RuleEngine.Engine.RuleSet;

namespace Winterflood.RuleEngine.Engine;

/// <summary>
/// Provides extension methods for adapting and composing rules in the Winterflood Rule Engine.
/// These extensions enable fluent rule composition and type adaptation.
/// </summary>
public static class RuleExtensions
{
    /// <summary>
    /// Converts an <see cref="IRuleSet{T}"/> to an <see cref="IRule{T}"/>, allowing rulesets
    /// to be used anywhere individual rules are expected.
    /// </summary>
    /// <typeparam name="T">
    /// The type of rule data. Must be a class implementing <see cref="IRuleData"/>
    /// with a parameterless constructor.
    /// </typeparam>
    /// <param name="ruleset">The ruleset to convert to a rule.</param>
    /// <returns>
    /// An <see cref="IRule{T}"/> that wraps the ruleset while maintaining all its evaluation behavior.
    /// </returns>
    /// <example>
    /// <code>
    /// var rule = myRuleset.AsRule();
    /// </code>
    /// </example>
    public static IRule<T> AsRule<T>(this IRuleSet<T> ruleset)
        where T : class, IRuleData, new()
        => new RulesetAsRuleAdapter<T>(ruleset);

    /// <summary>
    /// Adapts a rule to evaluate collections of items, applying the rule to each item in the collection.
    /// </summary>
    /// <typeparam name="T">
    /// The type of items in the collection.
    /// </typeparam>
    /// <param name="rule">The rule to apply to each collection item.</param>
    /// <returns>
    /// An <see cref="IRule{T}"/> where T is <see cref="IEnumerable{T}"/> that evaluates each item
    /// in the input collection.
    /// </returns>
    /// <example>
    /// <code>
    /// var collectionRule = itemRule.ForCollection();
    /// bool allValid = collectionRule.Evaluate(items, context);
    /// </code>
    /// </example>
    public static IRule<IEnumerable<T>> ForCollection<T>(this IRule<T> rule)
        where T : class, IRuleData, new()
        => new CollectionRuleAdapter<T>(rule);

    /// <summary>
    /// Creates a binding between different data types, enabling a rule to operate on a transformed
    /// version of the input data.
    /// </summary>
    /// <typeparam name="TSource">
    /// The source data type. Must be a class implementing <see cref="IRuleData"/>
    /// with a parameterless constructor.
    /// </typeparam>
    /// <typeparam name="TTarget">
    /// The target data type the underlying rule expects.
    /// </typeparam>
    /// <param name="rule">The rule to bind to the source type.</param>
    /// <param name="bindingFactory">
    /// Factory method that converts source data to the target type expected by the rule.
    /// </param>
    /// <param name="afterExecute">
    /// Optional callback invoked after rule execution, receiving both the source and transformed target data.
    /// </param>
    /// <returns>
    /// An <see cref="IRule{T}"/> that operates on the source type by transforming it to the target type.
    /// </returns>
    /// <remarks>
    /// The binding factory is invoked for each evaluation, and the afterExecute callback (if provided)
    /// is called regardless of whether the evaluation succeeds or fails.
    /// </remarks>
    /// <example>
    /// <code>
    /// var boundRule = childRule.Bind&lt;Parent, Child&gt;(
    ///     parent => parent.Child,
    ///     (parent, child) => parent.LastStatus = child.Status);
    /// </code>
    /// </example>
    public static IRule<TSource> Bind<TSource, TTarget>(
        this IRule<TTarget> rule,
        Func<TSource, TTarget> bindingFactory,
        Action<TSource, TTarget>? afterExecute = null)
        where TSource : class, IRuleData, new()
        where TTarget : class
        => new RuleBinder<TSource, TTarget>(rule, bindingFactory, afterExecute);
}