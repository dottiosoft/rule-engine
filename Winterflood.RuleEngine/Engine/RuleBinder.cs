using Winterflood.RuleEngine.Engine.Context;
using Winterflood.RuleEngine.Engine.Data;
using Winterflood.RuleEngine.Engine.Rule;

namespace Winterflood.RuleEngine.Engine;

/// <summary>
/// Binds a rule of type <typeparamref name="TTargetData"/> to operate on type <typeparamref name="TSourceData"/>.
/// Handles data transformation, execution hooks, and context management between source and target types.
/// </summary>
/// <typeparam name="TSourceData">The source data type. Must implement <see cref="IRuleData"/>.</typeparam>
/// <typeparam name="TTargetData">The target data type the underlying rule expects.</typeparam>
public class RuleBinder<TSourceData, TTargetData> : IRule<TSourceData>
    where TSourceData : class, IRuleData, new()
    where TTargetData : class
{
    private readonly IRule<TTargetData> _targetRule;
    private readonly Func<TSourceData, TTargetData> _bindingFactory;
    private readonly Action<TSourceData, TTargetData>? _afterExecution;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuleBinder{TSourceData, TTargetData}"/> class.
    /// </summary>
    /// <param name="targetRule">The rule to bind to the source type.</param>
    /// <param name="bindingFactory">Factory method to convert source to target data.</param>
    /// <param name="afterExecution">Optional callback after rule execution.</param>
    public RuleBinder(
        IRule<TTargetData> targetRule,
        Func<TSourceData, TTargetData> bindingFactory,
        Action<TSourceData, TTargetData>? afterExecution = null)
    {
        _targetRule = targetRule ?? throw new ArgumentNullException(nameof(targetRule));
        _bindingFactory = bindingFactory ?? throw new ArgumentNullException(nameof(bindingFactory));
        _afterExecution = afterExecution;
    }

    /// <inheritdoc/>
    public string Name => _targetRule.Name;

    /// <inheritdoc/>
    public bool Evaluate(TSourceData source, RootContext rootContext)
    {
        var targetData = _bindingFactory(source);

        var result = _targetRule.Evaluate(targetData, rootContext);

        _afterExecution?.Invoke(source, targetData);

        return result;
    }

    /// <inheritdoc/>
    public object Success(TSourceData input, RootContext rootContext)
    {
        return true;
    }

    /// <inheritdoc/>
    public object Failure(TSourceData input, RootContext rootContext)
    {
        return false;
    }
}