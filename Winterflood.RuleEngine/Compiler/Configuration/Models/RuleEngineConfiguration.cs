namespace Winterflood.RuleEngine.Compiler.Configuration.Models;

/// <summary>
/// 
/// </summary>
public class RuleEngineConfiguration
{
    public List<DataModelDefinition> Types { get; set; } = [];
    public List<RuleSetDefinition> RuleSets { get; set; } = [];
}