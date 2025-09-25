using Microsoft.Extensions.Logging;
using Winterflood.RuleEngine.Engine.Data;
using Winterflood.RuleEngine.Engine.Rule;
using Winterflood.RuleEngine.Engine.RuleSet;

namespace Winterflood.RuleEngine.Engine;

/// <summary>
/// Provides a fluent builder for creating and configuring a <see cref="RuleSet{TData}"/>.
/// </summary>
/// <typeparam name="TData">The input data type for the ruleset.</typeparam>
public sealed class RuleSetBuilder<TData>
    where TData : class, IRuleData, new()
{
    private readonly List<Func<IRule<TData>>> _rules = [];
    private readonly string _name;
    private readonly RuleExecutionMode _mode;
    private readonly ILoggerFactory _loggerFactory;

    private RuleSetBuilder(string name, RuleExecutionMode mode, ILoggerFactory loggerFactory)
    {
        _name = name;
        _mode = mode;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Creates a new builder instance.
    /// </summary>
    public static RuleSetBuilder<TData> Create(string name, RuleExecutionMode mode,
        ILoggerFactory loggerFactory)
        => new(name, mode, loggerFactory);

    /// <summary>
    /// Adds a rule instance directly.
    /// </summary>
    public RuleSetBuilder<TData> Add(IRule<TData> rule)
    {
        _rules.Add(() => rule);
        return this;
    }

    /// <summary>
    /// Adds a rule by type (must have parameterless constructor).
    /// </summary>
    public RuleSetBuilder<TData> Add<TRule>() where TRule : IRule<TData>, new()
    {
        _rules.Add(() => new TRule());
        return this;
    }

    /// <summary>
    /// Adds a rule using a factory function.
    /// </summary>
    public RuleSetBuilder<TData> Add(Func<IRule<TData>> factory)
    {
        _rules.Add(factory);
        return this;
    }

    /// <summary>
    /// Builds the configured RuleSet.
    /// </summary>
    public RuleSet<TData> Build()
    {
        var ruleSet = new RuleSet<TData>(_name, _mode, _loggerFactory);
        foreach (var ruleFactory in _rules)
            ruleSet.AddRule(ruleFactory);
        return ruleSet;
    }
}