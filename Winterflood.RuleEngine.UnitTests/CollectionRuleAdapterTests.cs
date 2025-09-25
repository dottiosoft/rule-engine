using Microsoft.Extensions.Logging;
using Moq;
using Winterflood.RuleEngine.Engine.Adapter;
using Winterflood.RuleEngine.Engine.Context;
using Winterflood.RuleEngine.Engine.Data;
using Winterflood.RuleEngine.Engine.Rule;
using Xunit;
using Assert = Xunit.Assert;

namespace Winterflood.RuleEngine.UnitTests;

public class CollectionRuleAdapterTests
{
    public class TestData : IRuleData
    {
        public int Value { get; set; }
    }

    private static Mock<ILoggerFactory> CreateLoggerFactory(out Mock<ILogger> loggerMock)
    {
        loggerMock = new Mock<ILogger>();
        var factoryMock = new Mock<ILoggerFactory>();
        factoryMock
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(loggerMock.Object);
        return factoryMock;
    }

    [Fact]
    public void Name_AppendsCollectionSuffix()
    {
        var ruleMock = new Mock<IRule<TestData>>();
        ruleMock.Setup(r => r.Name).Returns("SimpleRule");

        var adapter = new CollectionRuleAdapter<TestData>(ruleMock.Object);

        Assert.Equal("SimpleRule[Collection]", adapter.Name);
    }

    [Fact]
    public void Evaluate_AllItemsPass_ReturnsTrue()
    {
        var ruleMock = new Mock<IRule<TestData>>();
        ruleMock.Setup(r => r.Name).Returns("PassRule");
        ruleMock.Setup(r => r.Evaluate(It.IsAny<TestData>(), It.IsAny<RootContext>())).Returns(true);

        var loggerFactory = CreateLoggerFactory(out _).Object;
        var adapter = new CollectionRuleAdapter<TestData>(ruleMock.Object, loggerFactory);

        var items = Enumerable.Range(0, 5).Select(i => new TestData { Value = i });
        var rootContext = new RootContext();

        var result = adapter.Evaluate(items, rootContext);

        Assert.True(result);
        Assert.Equal(5, rootContext.ChildContexts.Count);
    }

    [Fact]
    public void Evaluate_SomeItemsFail_ReturnsFalse()
    {
        var ruleMock = new Mock<IRule<TestData>>();
        ruleMock.Setup(r => r.Name).Returns("FailingRule");
        ruleMock
            .SetupSequence(r => r.Evaluate(It.IsAny<TestData>(), It.IsAny<RootContext>()))
            .Returns(true)
            .Returns(false)
            .Returns(true);

        var loggerFactory = CreateLoggerFactory(out _).Object;
        var adapter = new CollectionRuleAdapter<TestData>(ruleMock.Object, loggerFactory);

        var items = new List<TestData>
        {
            new() { Value = 1 },
            new() { Value = 2 },
            new() { Value = 3 }
        };
        var rootContext = new RootContext();

        var result = adapter.Evaluate(items, rootContext);

        Assert.False(result);
        Assert.Equal(3, rootContext.ChildContexts.Count);
    }

    [Fact]
    public void Evaluate_TracksIndividualContexts()
    {
        var ruleMock = new Mock<IRule<TestData>>();
        ruleMock.Setup(r => r.Name).Returns("TrackingRule");
        ruleMock.Setup(r => r.Evaluate(It.IsAny<TestData>(), It.IsAny<RootContext>())).Returns(true);

        var adapter = new CollectionRuleAdapter<TestData>(ruleMock.Object);

        var items = Enumerable.Range(0, 3).Select(i => new TestData { Value = i });
        var rootContext = new RootContext();

        var result = adapter.Evaluate(items, rootContext);

        Assert.True(result);
        Assert.True(rootContext.ChildContexts.ContainsKey("TrackingRule[0]"));
        Assert.True(rootContext.ChildContexts.ContainsKey("TrackingRule[1]"));
        Assert.True(rootContext.ChildContexts.ContainsKey("TrackingRule[2]"));
    }

    [Fact]
    public void Success_ReturnsTrue()
    {
        var ruleMock = new Mock<IRule<TestData>>();
        ruleMock.Setup(r => r.Name).Returns("SuccessRule");

        var adapter = new CollectionRuleAdapter<TestData>(ruleMock.Object);

        var result = adapter.Success(new List<TestData>(), new RootContext());

        Assert.True((bool)result);
    }

    [Fact]
    public void Failure_ReturnsFalse()
    {
        var ruleMock = new Mock<IRule<TestData>>();
        ruleMock.Setup(r => r.Name).Returns("FailureRule");

        var adapter = new CollectionRuleAdapter<TestData>(ruleMock.Object);

        var result = adapter.Failure(new List<TestData>(), new RootContext());

        Assert.False((bool)result);
    }

    [Fact]
    public void Constructor_ThrowsIfRuleIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new CollectionRuleAdapter<TestData>(null!));
    }
}