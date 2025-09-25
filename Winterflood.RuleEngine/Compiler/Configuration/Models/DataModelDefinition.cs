namespace Winterflood.RuleEngine.Compiler.Configuration.Models;

/// <summary>
/// Represents a data model definition with fields.
/// </summary>
public class DataModelDefinition
{
    /// <summary>
    /// Gets or sets the name of the data model.
    /// </summary>
    public string Name { get; set; } = $"_data_model_{Guid.NewGuid()}";
    
    /// <summary>
    /// Gets or sets the list of field definitions.
    /// </summary>
    public List<FieldDefinition> Fields { get; set; } = [];
}