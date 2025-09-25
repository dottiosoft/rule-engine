using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Winterflood.RuleEngine.Compiler.Configuration.Models;
using Winterflood.RuleEngine.Engine.Context;
using System.Linq.Expressions;
using System.Linq.Dynamic.Core;

namespace Winterflood.RuleEngine.Compiler.Runners;

/// <summary>
/// Provides functionality to dynamically execute and validate tests defined against compiled rule sets.
/// </summary>
public static class TestRunner
{
    /// <summary>
    /// Executes all defined rule set tests within the specified configuration against the compiled assembly.
    /// </summary>
    public static void RunTests(
        Assembly compiledAssembly,
        RuleEngineConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(TestRunner));

        foreach (var ruleSet in configuration.RuleSets)
        {
            if (ruleSet.Tests.Count == 0)
            {
                logger.LogWarning("No tests defined for RuleSet={RuleSetName}. Skipping...", ruleSet.Name);
                continue;
            }

            logger.LogInformation("Running tests for RuleSet={RuleSetName}", ruleSet.Name);

            var ruleSetInstance = CompiledRuleSetResolver.TryResolveRuleSet(
                compiledAssembly,
                ruleSet.Name,
                logger,
                loggerFactory
            );

            if (ruleSetInstance is null)
                continue;

            var evaluateMethod = EvaluationExecutor.ResolveEvaluateMethod(
                ruleSetInstance,
                logger,
                ruleSet.Name
            );

            if (evaluateMethod is null)
                continue;

            var dataParamType = evaluateMethod.GetParameters()[0].ParameterType;

            foreach (var test in ruleSet.Tests)
            {
                logger.LogInformation("Running test for RuleSet={RuleSetName}", ruleSet.Name);

                object? testData;
                try
                {
                    testData = JsonSerializer.Deserialize(
                        test.Data.ToString(),
                        dataParamType,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                            Converters = { new JsonStringEnumConverter() }
                        });

                    if (testData == null)
                    {
                        logger.LogError("Failed to deserialize test data: RuleSet={RuleSetName}", ruleSet.Name);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "JSON deserialization failed for RuleSet={RuleSetName}", ruleSet.Name);
                    continue;
                }

                var context = new RootContext();

                try
                {
                    var success =
                        EvaluationExecutor.TryInvokeEvaluate(
                            evaluateMethod,
                            ruleSetInstance,
                            testData,
                            context,
                            logger
                        );

                    var passed = success && EvaluatePredicate(dataParamType, testData, test.Expect!);

                    if (passed)
                    {
                        logger.LogInformation("[Test Passed] RuleSet={RuleSetName}", ruleSet.Name);
                    }
                    else
                    {
                        var serializedData =
                            JsonSerializer.Serialize(
                                testData,
                                new JsonSerializerOptions { WriteIndented = true });

                        logger.LogError(
                            "[Test Failed] RuleSet={RuleSetName} Predicate={Predicate} Data={SerializedData}",
                            ruleSet.Name,
                            test.Expect,
                            serializedData
                        );
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Exception during evaluation of RuleSet={RuleSetName}", ruleSet.Name);
                }
            }
        }
    }

    /// <summary>
    /// Dynamically compiles and evaluates a predicate against a given object instance.
    /// </summary>
    private static bool EvaluatePredicate(Type modelType, object model, string expression)
    {
        try
        {
            var parameter = Expression.Parameter(modelType, "data");

            var lambda =
                DynamicExpressionParser.ParseLambda(
                    [parameter],
                    typeof(bool),
                    expression);

            var compiledDelegate = lambda.Compile();
            return (bool)compiledDelegate.DynamicInvoke(model)!;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to evaluate predicate '{expression}' for type {modelType.Name}: {ex.Message}");
            return false;
        }
    }
}