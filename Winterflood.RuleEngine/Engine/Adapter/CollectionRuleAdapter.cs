using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Winterflood.RuleEngine.Engine.Context;
using Winterflood.RuleEngine.Engine.Data;
using Winterflood.RuleEngine.Engine.Rule;

namespace Winterflood.RuleEngine.Engine.Adapter;

/// <summary>
/// Adapts an <see cref="IRule{T}"/> to operate on collections of type <typeparamref name="T"/>.
/// Enables evaluation of multiple items using a single rule definition while maintaining individual evaluation contexts.
/// </summary>
/// <typeparam name="T">The type of data the rule evaluates. Must be a class implementing <see cref="IRuleData"/> with a parameterless constructor.</typeparam>
public class CollectionRuleAdapter<T> : IRule<IEnumerable<T>> where T : class, IRuleData, new()
{
    private readonly IRule<T> _rule;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CollectionRuleAdapter{T}"/> class.
    /// </summary>
    /// <param name="rule">The rule to be adapted for collection evaluation.</param>
    /// <param name="loggerFactory">Optional logger factory for creating loggers.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
    public CollectionRuleAdapter(IRule<T> rule, ILoggerFactory? loggerFactory = null)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        _logger =
            (ILogger?)loggerFactory?.CreateLogger<CollectionRuleAdapter<T>>()
            ?? NullLogger.Instance;
    }

    /// <summary>
    /// Gets the name of the adapted rule, indicating collection handling.
    /// </summary>
    /// <value>The underlying rule's name suffixed with "[Collection]".</value>
    public string Name => $"{_rule.Name}[Collection]";

    /// <summary>
    /// Evaluates all items in the collection using the adapted rule.
    /// </summary>
    /// <param name="items">The collection of items to evaluate.</param>
    /// <param name="rootContext">The ruleset execution context for tracking evaluation state.</param>
    /// <returns>
    /// <c>true</c> if all items in the collection pass the rule evaluation; 
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// <para>Evaluation short-circuits on the first failure when the context's execution mode is <see cref="RuleExecutionMode.StopOnFirstFailure"/>.</para>
    /// <para>Each item evaluation is tracked individually in the context with a unique key combining the rule name and item index.</para>
    /// </remarks>
    public bool Evaluate(IEnumerable<T> items, RootContext rootContext)
    {
        _logger.LogInformation("[Evaluating] Collection Rule={RuleName}", Name);

        var itemsList = items.ToList();
        var success = true;
        var index = 0;

        foreach (var item in itemsList)
        {
            var ctx = new RootContext();
            rootContext.SetChildContext($"{_rule.Name}[{index}]", ctx);
            
            var passed = _rule.Evaluate(item, ctx);
            
            _logger.LogInformation(
                "[{Status}] Collection Item {Index} for Rule={RuleName}",
                passed ? "PASSED" : "FAILED",
                index,
                _rule.Name);

            success &= passed;
            index++;
        }

        _logger.LogInformation(
            "[Evaluated] Collection Rule={RuleName} Result={Result} Items={ItemCount}",
            Name,
            success,
            itemsList.Count);

        return success;
    }

    /// <summary>
    /// Handles successful evaluation of the collection.
    /// </summary>
    /// <param name="input">The evaluated collection.</param>
    /// <param name="rootContext">The ruleset execution context containing all item evaluations.</param>
    /// <returns>Returns the execution context containing all item evaluation results.</returns>
    public object Success(IEnumerable<T> input, RootContext rootContext)
    {
        _logger.LogInformation("[Success] Collection Rule={RuleName}", Name);
        return true;
    }

    /// <summary>
    /// Handles failed evaluation of the collection.
    /// </summary>
    /// <param name="input">The evaluated collection.</param>
    /// <param name="rootContext">The ruleset execution context containing all item evaluations.</param>
    /// <returns>Returns the execution context containing all item evaluation results.</returns>
    public object Failure(IEnumerable<T> input, RootContext rootContext)
    {
        _logger.LogInformation("[Failure] Collection Rule={RuleName}", Name);
        return false;
    }
}