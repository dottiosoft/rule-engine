namespace Winterflood.RuleEngine.Engine.Context;

/// <summary>
/// Represents the execution context for a single rule evaluation.
/// Tracks the rule's execution details, including input data, output states,
/// evaluation results, and final output values.
/// </summary>
public class RuleContext
{
    /// <summary>
    /// The name of the rule being evaluated.
    /// Helps in identifying the rule when logging execution results
    /// or referencing output and context.
    /// </summary>
    public string? RuleName { get; set; }

    /// <summary>
    /// The state of the data object before rule evaluation.
    /// Allows tracking changes made by the rule.
    /// </summary>
    public object? RuleDataBeforeEvaluation { get; set; }

    /// <summary>
    /// The state of the data object after rule evaluation.
    /// Useful for comparing changes applied by the rule logic.
    /// </summary>
    public object? RuleDataAfterEvaluation { get; set; }

    /// <summary>
    /// Indicates whether the rule evaluation was successful.
    /// True if the rule passed, false otherwise.
    /// </summary>
    public bool Result { get; set; }

    /// <summary>
    /// The final computed output of the rule, if applicable.
    /// This could be any object or value returned by the rule execution.
    /// </summary>
    public object? Output { get; set; }

    /// <inheritdoc />
    public override string ToString()
        => $"RuleName={RuleName}, RuleDataBeforeEvaluation={RuleDataBeforeEvaluation}, RuleDataAfterEvaluation={RuleDataAfterEvaluation}, Result={Result}";
}