using Winterflood.RuleEngine.Engine.Context;
using Winterflood.RuleEngine.Engine.Data;
using Winterflood.RuleEngine.Engine.Rule;
using Xunit;
using Assert = Xunit.Assert;

namespace Winterflood.RuleEngine.UnitTests;

public class RuleTests
{
    private class TestData : IRuleData
    {
        public bool SuccessTriggered { get; set; }
        public bool FailureTriggered { get; set; }
        public int Value { get; set; }
    }

    [Fact]
    public void Rule_WithOnlyOnSuccess_AlwaysPasses()
    {
        var data = new TestData();
        var rule = new Rule<TestData>("AlwaysPass", (d, _) => d.SuccessTriggered = true);

        var result = rule.Evaluate(data, new RootContext());
        Assert.True(result);

        var output = rule.Success(data, new RootContext());
        Assert.True((bool)output);
        Assert.True(data.SuccessTriggered);
    }

    [Fact]
    public void Rule_WithCondition_ExecutesOnlyWhenTrue()
    {
        var data = new TestData();
        var rule = new Rule<TestData>(
            "Conditional",
            (d, _) => d.Value > 10,
            (d, _) => d.SuccessTriggered = true
        );

        data.Value = 5;
        var result = rule.Evaluate(data, new RootContext());
        Assert.False(result);
        Assert.False(data.SuccessTriggered);

        data.Value = 15;
        result = rule.Evaluate(data, new RootContext());
        Assert.True(result);
        rule.Success(data, new RootContext());
        Assert.True(data.SuccessTriggered);
    }

    [Fact]
    public void Rule_WithFailureHandler_InvokesOnFailure()
    {
        var data = new TestData();
        var rule = new Rule<TestData>(
            "FailureCase",
            (d, _) => false,
            (d, _) => d.SuccessTriggered = true,
            (d, _) => d.FailureTriggered = true
        );

        var result = rule.Evaluate(data, new RootContext());
        Assert.False(result);
        var output = rule.Failure(data, new RootContext());
        Assert.False((bool)output);
        Assert.True(data.FailureTriggered);
    }

    [Fact]
    public void Rule_FailureHandler_IsOptional()
    {
        var data = new TestData();
        var rule = new Rule<TestData>(
            "NoFailureHandler",
            (d, _) => false,
            (d, _) => d.SuccessTriggered = true
        );

        var result = rule.Evaluate(data, new RootContext());
        Assert.False(result);
        var output = rule.Failure(data, new RootContext());
        Assert.False((bool)output);
    }

    [Fact]
    public void Rule_Name_IsAssignedCorrectly()
    {
        var rule = new Rule<TestData>("MyRule", (d, _) => { });
        Assert.Equal("MyRule", rule.Name);
    }
}