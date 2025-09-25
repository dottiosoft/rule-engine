using Microsoft.Extensions.Logging;
using Moq;
using Winterflood.RuleEngine.Engine;
using Winterflood.RuleEngine.Engine.Context;
using Winterflood.RuleEngine.Engine.Data;
using Winterflood.RuleEngine.Engine.Rule;
using Winterflood.RuleEngine.Engine.RuleSet;
using Xunit;
using Assert = Xunit.Assert;

namespace Winterflood.RuleEngine.UnitTests;

public class RuleSetTests
{
    private class TestData : IRuleData
    {
        public string Status { get; set; } = "";
        public int Counter { get; set; } = 0;
    }

    private static ILoggerFactory CreateLoggerFactory()
    {
        var loggerMock = new Mock<ILogger>();
        var factoryMock = new Mock<ILoggerFactory>();
        factoryMock
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(loggerMock.Object);
        return factoryMock.Object;
    }

    [Fact]
    public void Evaluate_AllRulesPass_ReturnsTrue()
    {
        var loggerFactory = CreateLoggerFactory();
        var ruleSet = new RuleSet<TestData>("TestSet", RuleExecutionMode.All, loggerFactory);

        ruleSet.AddRule(() => new Rule<TestData>("Rule1", (_, _) => true, (data, _) => data.Status = "OK"));
        ruleSet.AddRule(() => new Rule<TestData>("Rule2", (_, _) => true, (data, _) => data.Counter++));

        var data = new TestData();
        var context = new RootContext();

        var result = ruleSet.Evaluate(data, context);

        Assert.True(result);
        Assert.Equal("OK", data.Status);
        Assert.Equal(1, data.Counter);
    }

    [Fact]
    public void Evaluate_StopOnFirstFailure_ReturnsFalseAndStopsEarly()
    {
        var loggerFactory = CreateLoggerFactory();
        var ruleSet = new RuleSet<TestData>("FailEarlySet", RuleExecutionMode.StopOnFirstFailure, loggerFactory);

        ruleSet.AddRule(() =>
            new Rule<TestData>("Rule1", (_, _) => false, (_, _) => { }, (data, _) => { data.Counter = -1; }));
        ruleSet.AddRule(() => new Rule<TestData>("Rule2", (_, _) => true, (data, _) => data.Status = "SHOULD_NOT_RUN"));

        var data = new TestData();
        var context = new RootContext();

        var result = ruleSet.Evaluate(data, context);

        Assert.False(result);
        Assert.Equal(-1, data.Counter);
        Assert.Equal("", data.Status); // Should not execute
    }

    [Fact]
    public void Evaluate_StopOnFirstSuccess_ReturnsTrueAndStopsEarly()
    {
        var loggerFactory = CreateLoggerFactory();
        var ruleSet = new RuleSet<TestData>("SuccessEarlySet", RuleExecutionMode.StopOnFirstSuccess, loggerFactory);

        ruleSet.AddRule(() => new Rule<TestData>("Rule1", (_, _) => true, (data, _) => data.Status = "PASS"));
        ruleSet.AddRule(() => new Rule<TestData>("Rule2", (_, _) => true, (data, _) => data.Status = "SHOULD_NOT_RUN"));

        var data = new TestData();
        var context = new RootContext();

        var result = ruleSet.Evaluate(data, context);

        Assert.True(result);
        Assert.Equal("PASS", data.Status);
    }

    [Fact]
    public void Evaluate_AllRulesFail_ReturnsFalse()
    {
        var loggerFactory = CreateLoggerFactory();
        var ruleSet = new RuleSet<TestData>("AllFailSet", RuleExecutionMode.All, loggerFactory);

        ruleSet.AddRule(() => new Rule<TestData>("Rule1", (_, _) => false, (_, _) => { }, (data, _) => data.Counter--));
        ruleSet.AddRule(() => new Rule<TestData>("Rule2", (_, _) => false, (_, _) => { }, (data, _) => data.Counter--));

        var data = new TestData();
        var context = new RootContext();

        var result = ruleSet.Evaluate(data, context);

        Assert.False(result);
        Assert.Equal(-2, data.Counter);
    }

    [Fact]
    public void Success_ReturnsTrue()
    {
        var ruleSet = new RuleSet<TestData>("Test", RuleExecutionMode.All, CreateLoggerFactory());

        var result = ruleSet.Success(new TestData(), new RootContext());

        Assert.True((bool)result);
    }

    [Fact]
    public void Failure_ReturnsFalse()
    {
        var ruleSet = new RuleSet<TestData>("Test", RuleExecutionMode.All, CreateLoggerFactory());

        var result = ruleSet.Failure(new TestData(), new RootContext());

        Assert.False((bool)result);
    }

    [Fact]
    public void Context_ShouldContainAllRuleContexts()
    {
        var loggerFactory = CreateLoggerFactory();
        var ruleSet = new RuleSet<TestData>("ContextCheck", RuleExecutionMode.All, loggerFactory);

        ruleSet.AddRule(() => new Rule<TestData>("RuleA", (_, _) => true, (_, _) => { }));
        ruleSet.AddRule(() => new Rule<TestData>("RuleB", (_, _) => false, (_, _) => { }, (_, _) => { }));

        var context = new RootContext();
        var result = ruleSet.Evaluate(new TestData(), context);

        Assert.False(result);
        Assert.True(context.ChildContexts.ContainsKey("RuleA"));
        Assert.True(context.ChildContexts.ContainsKey("RuleB"));
    }
}