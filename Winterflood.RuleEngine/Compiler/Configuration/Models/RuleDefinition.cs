using System.Text.Json.Serialization;
using Winterflood.RuleEngine.Engine;

namespace Winterflood.RuleEngine.Compiler.Configuration.Models;

/// <summary>
/// 
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuleType
{
    /// <summary>
    /// 
    /// </summary>
    StandardRule,
    /// <summary>
    /// 
    /// </summary>
    NestedRuleSet
}
/// <summary>
/// Represents a definition of a ruleset, including its metadata, rules, and associated test cases.
/// </summary>
public class RuleSetDefinition
{
    /// <summary>
    /// Gets or sets the unique name of the ruleset.
    /// </summary>
    public string Name { get; set; } = $"_ruleset_{Guid.NewGuid()}";

    /// <summary>
    /// Gets or sets the name of the data type that this ruleset will operate on.
    /// </summary>
    public string DataType { get; set; } = "string";

    /// <summary>
    /// Gets or sets the execution mode that determines how rules are evaluated (e.g., All or First).
    /// </summary>
    public RuleExecutionMode ExecutionMode { get; set; } = RuleExecutionMode.All;

    /// <summary>
    /// Gets or sets the list of rules that this ruleset includes.
    /// </summary>
    public List<RuleDefinition> Rules { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of test cases associated with this ruleset.
    /// </summary>
    public List<RuleTestDefinition> Tests { get; set; } = [];
}

/// <summary>
/// Base class for all rule definitions, providing shared properties like rule name and adapters.
/// </summary>
public abstract class RuleDefinition
{
    /// <summary>
    /// Gets or sets the unique name of the rule.
    /// </summary>
    public string RuleName { get; set; } = $"_rule_{Guid.NewGuid()}";

    /// <summary>
    /// Gets or sets the ordered list of adapters to apply to this rule, such as AsRule, ForCollection, or Bind.
    /// </summary>
    public List<string> Adapters { get; set; } = [];

    /// <summary>
    /// Gets or sets the optional binding logic used if the Bind adapter is applied.
    /// </summary>
    public BindingAdapter? Binding { get; set; }
}

/// <summary>
/// Represents a standard rule that includes condition, success logic, and failure logic.
/// </summary>
public class StandardRuleDefinition : RuleDefinition
{
    /// <summary>
    /// Gets or sets the condition expression to evaluate.
    /// </summary>
    public string Conditions { get; set; } = "true";

    /// <summary>
    /// Gets or sets the code to execute when the condition evaluates to true.
    /// </summary>
    public string OnSuccess { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the code to execute when the condition evaluates to false.
    /// </summary>
    public string OnFailure { get; set; } = string.Empty;
}

/// <summary>
/// Represents a rule that delegates execution to a nested ruleset.
/// </summary>
public class NestedRuleDefinition : RuleDefinition
{
    /// <summary>
    /// Gets or sets the name of the ruleset to execute as a nested rule.
    /// </summary>
    public string RulesetName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the data type that the nested ruleset expects.
    /// </summary>
    public string DataType { get; set; } = "string";
}

/// <summary>
/// Represents optional binding logic applied to a rule or ruleset when using the Bind adapter.
/// </summary>
public class BindingAdapter
{
    /// <summary>
    /// Gets or sets the fully qualified type name of the child object to bind to.
    /// </summary>
    public string BindSourceType { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the fully qualified type name of the child object to bind to.
    /// </summary>
    public string BindTargetType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the lambda expression used to select the child data from the parent context.
    /// </summary>
    public string BindFactory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional lambda expression to run after rule execution completes.
    /// </summary>
    public string? AfterExecute { get; set; }
}
