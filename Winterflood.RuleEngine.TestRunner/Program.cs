// Program.cs

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Winterflood.RuleEngine.Compiler.Configuration.Models;
using Winterflood.RuleEngine.Engine;

namespace Winterflood.RuleEngine.TestRunner;

public class Program
{
    static void Main(string[] args)
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                options.SingleLine = true;
                options.TimestampFormat = "hh:mm:ss ";
            });
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var config = new RuleEngineConfiguration
        {
            Types =
            [
                // Customer
                new DataModelDefinition
                {
                    Name = "Customer",
                    Fields =
                    [
                        new FieldDefinition { Name = "Name", Type = "string" },
                        new FieldDefinition { Name = "TotalSpend", Type = "decimal" },
                        new FieldDefinition { Name = "IsHighValue", Type = "bool" }
                    ]
                },
                // Order
                new DataModelDefinition
                {
                    Name = "Order",
                    Fields =
                    [
                        new FieldDefinition { Name = "Customer", Type = "Customer" },
                        new FieldDefinition { Name = "IsHighValue", Type = "bool" }
                    ]
                }
            ],
            RuleSets =
            [
                // Customer
                new RuleSetDefinition
                {
                    Name = "CustomerEvaluation",
                    DataType = "Customer",
                    ExecutionMode = RuleExecutionMode.All,
                    Rules =
                    [
                        new StandardRuleDefinition
                        {
                            RuleName = "EvaluateSpend",
                            Conditions = "data.TotalSpend > 1000",
                            OnSuccess = "data.IsHighValue = true",
                            OnFailure = "data.IsHighValue = false"
                        }
                    ]
                },
                // Order
                new RuleSetDefinition
                {
                    Name = "OrderEvaluation",
                    DataType = "Order",
                    ExecutionMode = RuleExecutionMode.All,
                    Rules =
                    [
                        new NestedRuleDefinition
                        {
                            RulesetName = "CustomerEvaluation",
                            Adapters = ["AsRule", "Bind"],
                            Binding = new BindingAdapter
                            {
                                BindSourceType = "Order",
                                BindTargetType = "Customer",
                                BindFactory = "return sourceData.Customer",
                                AfterExecute = "sourceData.IsHighValue = targetData.IsHighValue;",
                            }
                        }
                    ],
                    Tests =
                    [
                        new RuleTestDefinition
                        {
                            Data = ToJsonElement(new
                            {
                                Customer = new
                                {
                                    Name = "Alice",
                                    TotalSpend = 1200m
                                }
                            }),
                            Expect = "data.Customer.IsHighValue == true && data.Customer.Name == \"Alice\""
                        },
                        new RuleTestDefinition
                        {
                            Data = ToJsonElement(new
                            {
                                Customer = new
                                {
                                    Name = "Bob",
                                    TotalSpend = 800m
                                }
                            }),
                            Expect = "data.Customer.IsHighValue == false"
                        }
                    ]
                }
            ]
        };

        var compiledAssembly =
            Compiler.Compiler.SyntaxTreeCompiler
                .Compile(config, loggerFactory).CompiledAssembly;

        Compiler.Runners.TestRunner.RunTests(compiledAssembly!, config, loggerFactory);
    }

    private static JsonElement ToJsonElement(object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}