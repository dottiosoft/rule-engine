using Moq;
using Winterflood.RuleEngine.Engine.Adapter;
using Winterflood.RuleEngine.Engine.Context;
using Winterflood.RuleEngine.Engine.Data;
using Winterflood.RuleEngine.Engine.RuleSet;
using Xunit;
using Assert = Xunit.Assert;

namespace Winterflood.RuleEngine.UnitTests;

public class RulesetAsRuleAdapterTests
{
    public class TestData : IRuleData
    {
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void Evaluate_WhenRulesetReturnsTrue_ShouldReturnTrueAndAddContext()
    {
        // Arrange
        var data = new TestData { Name = "Alice" };
        var rootContext = new RootContext();

        var rulesetMock = new Mock<IRuleSet<TestData>>();
        rulesetMock
            .Setup(r => r.Name)
            .Returns("NestedRuleset");
        rulesetMock
            .Setup(r => r.Evaluate(data, It.IsAny<RootContext>()))
            .Returns(true)
            .Callback<TestData, RootContext>((_, ctx) =>
            {
                ctx.SetChildContext("RuleA", new RuleContext { RuleName = "RuleA", Result = true });
            });

        var adapter = new RulesetAsRuleAdapter<TestData>(rulesetMock.Object);

        // Act
        var result = adapter.Evaluate(data, rootContext);

        // Assert
        Assert.True(result);
        Assert.True(rootContext.ChildContexts.ContainsKey("NestedRuleset"));
        var nested = rootContext.ChildContexts["NestedRuleset"] as RootContext;
        Assert.NotNull(nested);
        Assert.Contains("RuleA", nested!.ChildContexts.Keys);
    }

    [Fact]
    public void Evaluate_WhenRulesetReturnsFalse_ShouldReturnFalseAndAddContext()
    {
        // Arrange
        var data = new TestData { Name = "Bob" };
        var rootContext = new RootContext();

        var rulesetMock = new Mock<IRuleSet<TestData>>();
        rulesetMock
            .Setup(r => r.Name)
            .Returns("FailingRuleset");
        rulesetMock
            .Setup(r => r.Evaluate(data, It.IsAny<RootContext>()))
            .Returns(false);

        var adapter = new RulesetAsRuleAdapter<TestData>(rulesetMock.Object);

        // Act
        var result = adapter.Evaluate(data, rootContext);

        // Assert
        Assert.False(result);
        Assert.True(rootContext.ChildContexts.ContainsKey("FailingRuleset"));
        var nested = rootContext.ChildContexts["FailingRuleset"] as RootContext;
        Assert.NotNull(nested);
    }

    [Fact]
    public void Success_ShouldReturnTrueAndLog()
    {
        // Arrange
        var data = new TestData();
        var rootContext = new RootContext();

        var rulesetMock = new Mock<IRuleSet<TestData>>();
        rulesetMock.Setup(r => r.Name).Returns("SuccessSet");

        var adapter = new RulesetAsRuleAdapter<TestData>(rulesetMock.Object);

        // Act
        var result = adapter.Success(data, rootContext);

        // Assert
        Assert.True((bool)result);
    }

    [Fact]
    public void Failure_ShouldReturnFalseAndLog()
    {
        // Arrange
        var data = new TestData();
        var rootContext = new RootContext();

        var rulesetMock = new Mock<IRuleSet<TestData>>();
        rulesetMock.Setup(r => r.Name).Returns("FailureSet");

        var adapter = new RulesetAsRuleAdapter<TestData>(rulesetMock.Object);

        // Act
        var result = adapter.Failure(data, rootContext);

        // Assert
        Assert.False((bool)result);
    }

    [Fact]
    public void Name_ShouldReturnRulesetName()
    {
        // Arrange
        var rulesetMock = new Mock<IRuleSet<TestData>>();
        rulesetMock.Setup(r => r.Name).Returns("MyRuleset");

        var adapter = new RulesetAsRuleAdapter<TestData>(rulesetMock.Object);

        // Act
        var name = adapter.Name;

        // Assert
        Assert.Equal("MyRuleset", name);
    }

    [Fact]
    public void Constructor_WithNullRuleset_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => { _ = new RulesetAsRuleAdapter<TestData>(null!); });
    }
}