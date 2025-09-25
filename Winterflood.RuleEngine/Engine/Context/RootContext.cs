using System.Text.Json.Serialization;

namespace Winterflood.RuleEngine.Engine.Context;

/// <summary>
/// Represents the execution context for a ruleset.
/// Tracks rule execution results and stores the context of each evaluated rule.
/// </summary>
public class RootContext
{
    /// <summary>
    /// Dictionary storing execution contexts for rules evaluated within this ruleset.
    /// The key is the rule name, and the value is the execution context of that rule.
    /// </summary>
    [JsonInclude]
    public readonly Dictionary<string, object?> ChildContexts = new();

    /// <summary>
    /// Adds or updates the execution context for a specific rule.
    /// </summary>
    /// <typeparam name="T">The type of the rule's execution context.</typeparam>
    /// <param name="key">The unique key (rule name) identifying the rule.</param>
    /// <param name="context">The execution context object to store.</param>
    public void SetChildContext<T>(string key, T? context)
    {
        ChildContexts[key] = context;
    }

    /// <summary>
    /// Retrieves the execution context for a specific rule, if it exists.
    /// </summary>
    /// <typeparam name="T">The expected type of the stored execution context.</typeparam>
    /// <param name="key">The unique key (rule name) identifying the rule.</param>
    /// <returns>The execution context for the rule if found; otherwise, the default value of T.</returns>
    public T? GetChildContext<T>(string key)
    {
        if (ChildContexts.TryGetValue(key, out var context))
        {
            return context != null ? (T)context : default;
        }

        return default;
    }

    /// <summary>
    /// Checks whether a specific rule has an execution context stored.
    /// </summary>
    /// <param name="key">The unique key (rule name) identifying the rule.</param>
    /// <returns>True if the rule has an execution context; otherwise, false.</returns>
    public bool HasChildContext(string key)
    {
        return ChildContexts.ContainsKey(key);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return ChildContexts.Select(x => x.Value?.ToString() ?? "null").Aggregate((a, b) => $"{a}, {b}");
    }
}
