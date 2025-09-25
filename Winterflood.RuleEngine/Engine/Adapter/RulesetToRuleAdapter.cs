using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Winterflood.RuleEngine.Engine.Context;
using Winterflood.RuleEngine.Engine.Data;
using Winterflood.RuleEngine.Engine.Rule;
using Winterflood.RuleEngine.Engine.RuleSet;

namespace Winterflood.RuleEngine.Engine.Adapter;

/// <summary>
/// Adapts an <see cref="IRuleSet{T}"/> to be used as an <see cref="IRule{T}"/>.
/// This wrapper maintains all ruleset functionality while allowing it to be used as a single rule.
/// </summary>
/// <typeparam name="T">
/// The type of rule data that the ruleset operates on.
/// Must be a class implementing <see cref="IRuleData"/> with a parameterless constructor.
/// </typeparam>
public class RulesetAsRuleAdapter<T> : IRule<T> where T : class, IRuleData, new()
{
    private readonly IRuleSet<T> _ruleset;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RulesetAsRuleAdapter{T}"/> class.
    /// </summary>
    /// <param name="ruleset">The ruleset to adapt as a rule.</param>
    /// <param name="loggerFactory">Optional logger factory for creating loggers.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ruleset"/> is null.</exception>
    public RulesetAsRuleAdapter(IRuleSet<T> ruleset, ILoggerFactory? loggerFactory = null)
    {
        _ruleset = ruleset ?? throw new ArgumentNullException(nameof(ruleset));
        _logger =
            (ILogger?)loggerFactory?.CreateLogger<RulesetAsRuleAdapter<T>>()
            ?? NullLogger.Instance;
    }

    /// <summary>
    /// Gets the name of the adapted ruleset.
    /// </summary>
    /// <value>
    /// The name of the underlying ruleset, maintaining the original ruleset's identity.
    /// </value>
    public string Name => _ruleset.Name;

    /// <summary>
    /// Evaluates the ruleset against the provided data.
    /// </summary>
    /// <param name="data">The input data to evaluate.</param>
    /// <param name="rootContext">The ruleset execution context for tracking evaluation state.</param>
    /// <returns>
    /// <c>true</c> if the ruleset evaluation succeeds according to its execution mode;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// <para>The evaluation follows the ruleset's <see cref="RuleExecutionMode"/> for behavior like short-circuiting.</para>
    /// <para>Detailed evaluation results for each contained rule can be found in the <paramref name="rootContext"/>.</para>
    /// </remarks>
    public bool Evaluate(T data, RootContext rootContext)
    {
        _logger.LogInformation("[Evaluating] RulesetAsRule={RuleName}", Name);

        var nestedContext = new RootContext();
        
        // Execute nested ruleset
        var result = _ruleset.Evaluate(data, nestedContext);

        rootContext.SetChildContext(_ruleset.Name, nestedContext);

        _logger.LogInformation(
            "[Evaluated] RulesetAsRule={RuleName} Result={Result}",
            Name,
            result ? "PASSED" : "FAILED");

        return result;
    }

    /// <summary>
    /// Handles successful evaluation of the ruleset.
    /// </summary>
    /// <param name="input">The evaluated input data.</param>
    /// <param name="rootContext">The ruleset execution context containing all rule evaluations.</param>
    /// <returns>Returns the execution context containing all evaluation details.</returns>
    /// <remarks>
    /// The context contains complete information about each rule's evaluation within the ruleset.
    /// </remarks>
    public object Success(T input, RootContext rootContext)
    {
        _logger.LogInformation("[Success] RulesetAsRule={RuleName}", Name);
        return true;
    }

    /// <summary>
    /// Handles failed evaluation of the ruleset.
    /// </summary>
    /// <param name="input">The evaluated input data.</param>
    /// <param name="rootContext">The ruleset execution context containing all rule evaluations.</param>
    /// <returns>Returns the execution context containing all evaluation details.</returns>
    /// <remarks>
    /// The context contains detailed failure information for each rule that failed within the ruleset.
    /// </remarks>
    public object Failure(T input, RootContext rootContext)
    {
        _logger.LogInformation("[Failure] RulesetAsRule={RuleName}", Name);
        return false;
    }
}