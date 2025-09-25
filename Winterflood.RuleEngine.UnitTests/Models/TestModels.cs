using Winterflood.RuleEngine.Engine;
using Winterflood.RuleEngine.Engine.Data;

namespace Winterflood.RuleEngine.UnitTests.Models;

public class TestRuleData : IRuleData
{
    public int Value { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    
    public TestRuleData Clone() => new()
    {
        Value = Value,
        Message = Message,
        IsValid = IsValid
    };
}