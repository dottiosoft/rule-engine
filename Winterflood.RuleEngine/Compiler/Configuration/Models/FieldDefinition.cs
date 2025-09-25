namespace Winterflood.RuleEngine.Compiler.Configuration.Models;

/// <summary>
/// Represents a field definition within a data model.
/// </summary>
public class FieldDefinition
{
    /// <summary>
    /// Gets or sets the name of the field.
    /// </summary>
    public string Name { get; set; } = $"_field_{Guid.NewGuid()}";

    /// <summary>
    /// Gets or sets the type of the field.
    /// </summary>
    public string Type { get; set; } = "string";
}