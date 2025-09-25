using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Winterflood.RuleEngine.Compiler.Configuration.Models;
using Winterflood.RuleEngine.Constants;
using Winterflood.RuleEngine.Engine;
using Winterflood.RuleEngine.Engine.Context;
using Winterflood.RuleEngine.Engine.Data;
using Winterflood.RuleEngine.Engine.Rule;
using Winterflood.RuleEngine.Engine.RuleSet;

namespace Winterflood.RuleEngine.Compiler.Compiler;

/// <summary>
/// Responsible for compiling syntax trees into an assembly dynamically for the rule engine.
/// </summary>
public static class SyntaxTreeCompiler
{
    /// <summary>
    /// Compiles the given rule engine configuration into an assembly.
    /// </summary>
    /// <param name="configuration">The configuration containing rule definitions and types.</param>
    /// <param name="loggerFactory">The logger factory for logging compilation details.</param>
    /// <returns>The compiled assembly containing the rule engine logic.</returns>
    public static CompilationResult Compile(
        RuleEngineConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(SyntaxTreeCompiler));
        var syntaxTrees = new List<CompilationUnitSyntax>();

        logger.LogInformation("Starting Rule Engine Compilation");

        try
        {
            var typeSyntaxTrees =
                configuration.Types
                    .Select(model =>
                        BuildTypeSyntaxTree(
                            model,
                            logger))
                    .ToList();

            syntaxTrees.AddRange(typeSyntaxTrees);

            Compile(syntaxTrees, logger);

            var ruleSyntaxTrees =
                configuration.RuleSets
                    .SelectMany(ruleSet =>
                        ruleSet.Rules
                            .OfType<StandardRuleDefinition>()
                            .Select(rule =>
                                BuildRuleSyntaxTree(
                                    rule.RuleName,
                                    ruleSet.DataType,
                                    rule.Conditions,
                                    rule.OnSuccess,
                                    rule.OnFailure,
                                    logger)));

            syntaxTrees.AddRange(ruleSyntaxTrees);

            Compile(syntaxTrees, logger);

            var ruleSetSyntaxTrees =
                configuration.RuleSets
                    .Select(ruleSet =>
                        BuildRuleSetSyntaxTree(
                            ruleSet.Name,
                            ruleSet.DataType,
                            ruleSet.ExecutionMode,
                            ruleSet.Rules,
                            logger));

            syntaxTrees.AddRange(ruleSetSyntaxTrees);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Critical failure during syntax tree generation.");
            throw;
        }

        return Compile(syntaxTrees, logger);
    }

    /// <summary>
    /// Compiles a collection of syntax trees into an assembly.
    /// </summary>
    /// <param name="compilationUnits">The syntax trees representing the code to be compiled.</param>
    /// <param name="logger">The logger instance for logging messages.</param>
    /// <returns>The compiled assembly.</returns>
    public static CompilationResult Compile(
        IEnumerable<CompilationUnitSyntax> compilationUnits,
        ILogger logger)
    {
        // Map SyntaxTree -> ClassName
        var syntaxTreeMap = new Dictionary<SyntaxTree, string>();
        var syntaxTrees = new List<SyntaxTree>();

        var unitResults =
            compilationUnits
                .Select(unit =>
                {
                    var className = ExtractClassName(unit) ?? "SyntaxTree";
                    var code = unit.NormalizeWhitespace().ToFullString();
                    var tree = CSharpSyntaxTree.Create(unit);

                    syntaxTrees.Add(tree);
                    syntaxTreeMap[tree] = className;

                    return new CompilationUnitResult(
                        type: className,
                        success: true,
                        message: "",
                        meta: code
                    );
                })
                .ToList();

        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ILoggerFactory).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IRule<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IEnumerable<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IList<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Dictionary<,>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(string).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Linq").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Core").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location)
        };

        var compilation =
            CSharpCompilation.Create(
                "DynamicRules",
                syntaxTrees,
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        // My best effort attempt at trying to catch errors 
        // and link to the relevant syntax tree
        var errorsByTree =
            result.Diagnostics
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .GroupBy(diagnostic => diagnostic.Location.SourceTree)
                .Where(group => group.Key is not null) // :( some cases we won't have a sourcetree it seems
                .ToDictionary(group => group.Key!, group => group.Select(d => d.ToString()).ToList());

        // Catching errors that couldn't be linked to a syntax tree
        var errorsNoTree =
            result.Diagnostics
                .Where(diagnostic =>
                    diagnostic is
                    {
                        Severity: DiagnosticSeverity.Error,
                        Location.SourceTree: null
                    })
                .Select(diagnostic => diagnostic.ToString())
                .ToList();

        unitResults =
            unitResults
                .Select(unit =>
                    errorsByTree.TryGetValue(
                        syntaxTreeMap.FirstOrDefault(x => x.Value == unit.Type).Key,
                        out var errors)
                        ? new CompilationUnitResult(
                            unit.Type,
                            false,
                            string.Join("\n", errors),
                            unit.Meta)
                        : new CompilationUnitResult(
                            unit.Type,
                            true,
                            "Compiled successfully",
                            unit.Meta))
                .ToList();

        if (errorsNoTree.Count != 0)
        {
            unitResults.AddRange(errorsNoTree.Select(x => new CompilationUnitResult(
                type: "UnmatchedSyntaxTree",
                success: false,
                message: x,
                meta: "***"
            )));
        }

        if (!result.Success)
        {
            logger.LogError("Compilation failed with ErrorCount={ErrorCount} errors.", errorsByTree.Count);
        }
        else
        {
            logger.LogInformation("Compilation successful.");
            ms.Seek(0, SeekOrigin.Begin);
            return new CompilationResult(unitResults, Assembly.Load(ms.ToArray()));
        }

        return new CompilationResult(unitResults, null);
    }

    /// <summary>
    /// Extracts the first class name found in the syntax tree.
    /// </summary>
    private static string? ExtractClassName(CompilationUnitSyntax unit)
    {
        var classDeclaration =
            unit
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault();

        return classDeclaration?.Identifier.Text;
    }

    /// <summary>
    /// Builds a syntax tree representation for a rule type.
    /// </summary>
    /// <param name="model">The data model definition.</param>
    /// <param name="logger">The logger instance.</param>
    /// <returns>A compiled syntax tree.</returns>
    private static CompilationUnitSyntax BuildTypeSyntaxTree(
        DataModelDefinition model,
        ILogger logger)
    {
        var typeName = model.Name;
        var fields = model.Fields;
        var baseInterface = nameof(IRuleData);

        logger.LogInformation("Building TypeName={TypeName}", typeName);

        var classDeclaration =
            SyntaxFactory
                .ClassDeclaration(typeName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(baseInterface)))
                .AddMembers(fields.Select(BuildPropertySyntax).ToArray<MemberDeclarationSyntax>());

        var constructor = BuildConstructorSyntax(typeName, fields);
        classDeclaration = classDeclaration.AddMembers(constructor);

        var namespaceDeclaration =
            SyntaxFactory
                .NamespaceDeclaration(SyntaxFactory.ParseName(CompilerArtifactConstants.CompilerGenerated))
                .AddMembers(classDeclaration);

        var syntaxTree =
            SyntaxFactory
                .CompilationUnit()
                .AddUsings(
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")),
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(EngineNamespaceConstants.ContextNamespace)),
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(EngineNamespaceConstants.DataNamespace)),
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(EngineNamespaceConstants.RuleNamespace))
                )
                .AddMembers(namespaceDeclaration)
                .NormalizeWhitespace();

        return syntaxTree;
    }

    /// <summary>
    /// Builds a syntax tree for a rule definition.
    /// </summary>
    /// <param name="ruleName">The name of the rule.</param>
    /// <param name="dataType">The input data type for the rule.</param>
    /// <param name="condition">The condition expression for the rule.</param>
    /// <param name="onSuccess">The success action expression.</param>
    /// <param name="onFailure">The failure action expression.</param>
    /// <param name="logger">The logger instance.</param>
    /// <returns>A compiled rule syntax tree.</returns>
    private static CompilationUnitSyntax BuildRuleSyntaxTree(
        string ruleName,
        string dataType,
        string condition,
        string onSuccess,
        string onFailure,
        ILogger logger)
    {
        logger.LogInformation("Building RuleName={RuleName}", ruleName);

        var conditionLambda =
            SyntaxFactory
                .ParenthesizedLambdaExpression()
                .WithParameterList(
                    SyntaxFactory.ParameterList(
                        SyntaxFactory.SeparatedList([
                            SyntaxFactory
                                .Parameter(SyntaxFactory.Identifier("data"))
                                .WithType(SyntaxFactory.ParseTypeName(dataType)),
                            SyntaxFactory
                                .Parameter(SyntaxFactory.Identifier("ctx"))
                                .WithType(SyntaxFactory.ParseTypeName(nameof(RootContext)))
                        ])
                    ))
                .WithExpressionBody(
                    string.IsNullOrWhiteSpace(condition)
                        ? SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression)
                        : SyntaxFactory.ParseExpression(condition));

        var successStatements = new List<StatementSyntax>();
        if (!string.IsNullOrWhiteSpace(onSuccess))
        {
            successStatements.AddRange(
                onSuccess
                    .Split(';')
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => SyntaxFactory.ParseStatement(line.Trim() + ";"))
            );
        }

        var successLambda =
            SyntaxFactory
                .ParenthesizedLambdaExpression()
                .WithParameterList(conditionLambda.ParameterList)
                .WithBlock(SyntaxFactory.Block(successStatements));

        var failureStatements = new List<StatementSyntax>();
        if (!string.IsNullOrWhiteSpace(onFailure))
        {
            failureStatements.AddRange(
                onFailure
                    .Split(';')
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => SyntaxFactory.ParseStatement(line.Trim() + ";"))
            );
        }

        var failureLambda =
            SyntaxFactory
                .ParenthesizedLambdaExpression()
                .WithParameterList(conditionLambda.ParameterList)
                .WithBlock(SyntaxFactory.Block(failureStatements));

        var ruleClass =
            SyntaxFactory
                .ClassDeclaration(ruleName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .WithBaseList(SyntaxFactory.BaseList(
                    SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                        SyntaxFactory
                            .SimpleBaseType(
                                SyntaxFactory
                                    .GenericName(SyntaxFactory.Identifier(typeof(Rule<>).Name.Split('`')[0]))
                                    .WithTypeArgumentList(
                                        SyntaxFactory.TypeArgumentList(
                                            SyntaxFactory.SeparatedList(
                                            [
                                                SyntaxFactory.ParseTypeName(dataType)
                                            ])))))
                ))
                .WithMembers(
                    SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
                        SyntaxFactory
                            .ConstructorDeclaration(ruleName)
                            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                            .WithInitializer(
                                SyntaxFactory.ConstructorInitializer(
                                    SyntaxKind.BaseConstructorInitializer,
                                    SyntaxFactory.ArgumentList(
                                        SyntaxFactory.SeparatedList([
                                            SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                                                SyntaxKind.StringLiteralExpression,
                                                SyntaxFactory.Literal(ruleName)
                                            )),
                                            SyntaxFactory.Argument(conditionLambda),
                                            SyntaxFactory.Argument(successLambda),
                                            SyntaxFactory.Argument(failureLambda)
                                        ])
                                    )
                                )
                            )
                            .WithBody(SyntaxFactory.Block())
                    )
                );

        var syntaxTree =
            SyntaxFactory
                .CompilationUnit()
                .AddUsings(
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")),
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(EngineNamespaceConstants.ContextNamespace)),
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(EngineNamespaceConstants.DataNamespace)),
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(EngineNamespaceConstants.RuleNamespace))
                )
                .AddMembers(
                    SyntaxFactory
                        .NamespaceDeclaration(SyntaxFactory.ParseName(CompilerArtifactConstants.CompilerGenerated))
                        .AddMembers(ruleClass)
                ).NormalizeWhitespace();

        return syntaxTree;
    }

    /// <summary>
    /// Builds a syntax tree for a rule set.
    /// </summary>
    /// <param name="ruleSetName">The name of the rule set.</param>
    /// <param name="dataType">The input data type for the rule set.</param>
    /// <param name="executionMode">The execution mode for the rule set.</param>
    /// <param name="rules">The list of rules in the rule set.</param>
    /// <param name="logger">The logger instance.</param>
    /// <returns>A compiled rule set syntax tree.</returns>
    private static CompilationUnitSyntax BuildRuleSetSyntaxTree(
        string ruleSetName,
        string dataType,
        RuleExecutionMode executionMode,
        List<RuleDefinition> rules,
        ILogger logger)
    {
        logger.LogInformation("Building RuleSet={RuleSet}", ruleSetName);

        const string rulesetVariableName = "ruleset";
        const string baseRuleSetImplementationType = "RuleSet";
        const string baseRuleSetInterfaceType = "IRuleSet";

        var rulesetDeclaration =
            SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory
                    .VariableDeclaration(SyntaxFactory.IdentifierName("var"))
                    .WithVariables(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory
                                .VariableDeclarator(rulesetVariableName)
                                .WithInitializer(
                                    SyntaxFactory.EqualsValueClause(
                                        SyntaxFactory
                                            // Init type RuleSet<{dataType}>()
                                            .ObjectCreationExpression(
                                                SyntaxFactory.ParseTypeName(
                                                    $"{baseRuleSetImplementationType}<{dataType}>"))
                                            .WithArgumentList(
                                                SyntaxFactory.ArgumentList(
                                                    SyntaxFactory.SeparatedList([
                                                        // Ruleset Name
                                                        SyntaxFactory.Argument(
                                                            SyntaxFactory.LiteralExpression(
                                                                SyntaxKind.StringLiteralExpression,
                                                                SyntaxFactory.Literal(ruleSetName))),
                                                        // Ruleset Execution Mode
                                                        SyntaxFactory.Argument(
                                                            SyntaxFactory.MemberAccessExpression(
                                                                SyntaxKind.SimpleMemberAccessExpression,
                                                                SyntaxFactory.IdentifierName("RuleExecutionMode"),
                                                                SyntaxFactory.IdentifierName(
                                                                    executionMode.ToString()))),
                                                        // Logger
                                                        SyntaxFactory.Argument(
                                                            SyntaxFactory.IdentifierName("loggerFactory"))
                                                    ]))))))));

        var statements = new List<StatementSyntax> { rulesetDeclaration };
        statements.AddRange(rules.Select(GenerateRuleStatement));
        statements.Add(SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName(rulesetVariableName)));

        var ruleSetMethod =
            SyntaxFactory
                .MethodDeclaration(
                    SyntaxFactory.ParseTypeName($"{baseRuleSetInterfaceType}<{dataType}>"),
                    "Create"
                )
                .AddModifiers(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                .AddParameterListParameters(
                    SyntaxFactory
                        .Parameter(SyntaxFactory.Identifier("loggerFactory"))
                        .WithType(SyntaxFactory.ParseTypeName("ILoggerFactory"))
                )
                .WithBody(SyntaxFactory.Block(statements));

        var ruleSetClass =
            SyntaxFactory
                .ClassDeclaration(ruleSetName)
                .AddModifiers(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                .AddMembers(ruleSetMethod);

        var syntaxTree =
            SyntaxFactory
                .CompilationUnit()
                .AddUsings(
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")),
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("Microsoft.Extensions.Logging")),
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(EngineNamespaceConstants.EngineNamespace)),
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(EngineNamespaceConstants.ContextNamespace)),
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(EngineNamespaceConstants.DataNamespace)),
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(EngineNamespaceConstants.RuleNamespace)),
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(EngineNamespaceConstants.RuleSetNamespace))
                )
                .AddMembers(
                    SyntaxFactory
                        .NamespaceDeclaration(
                            SyntaxFactory.ParseName(CompilerArtifactConstants.CompilerGenerated))
                        .AddMembers(ruleSetClass)
                )
                .NormalizeWhitespace();

        return syntaxTree;

        static StatementSyntax GenerateRuleStatement(RuleDefinition rule)
        {
            // Step 1: Create base rule expression
            ExpressionSyntax baseExpression = rule switch
            {
                StandardRuleDefinition =>
                    SyntaxFactory
                        .ObjectCreationExpression(SyntaxFactory.IdentifierName(rule.RuleName))
                        .WithArgumentList(SyntaxFactory.ArgumentList()),

                NestedRuleDefinition nested =>
                    SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName(nested.RulesetName),
                                SyntaxFactory.IdentifierName("Create")))
                        .WithArgumentList(SyntaxFactory.ArgumentList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Argument(SyntaxFactory.IdentifierName("loggerFactory"))))),

                _ => throw new InvalidOperationException($"Unsupported rule type: {rule.GetType().Name}")
            };

            // 2.1 Apply adapters
            if (rule.Adapters.Contains("AsRule"))
            {
                baseExpression =
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            baseExpression,
                            SyntaxFactory.IdentifierName("AsRule")));
            }

            // 2.2 Apply ForCollection if present
            if (rule.Adapters.Contains("ForCollection"))
            {
                baseExpression =
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            baseExpression,
                            SyntaxFactory.IdentifierName("ForCollection")));
            }

            // 2.3 Apply Bind if present and binding is configured
            if (rule.Adapters.Contains("Bind") && rule.Binding is not null)
            {
                if (string.IsNullOrWhiteSpace(rule.Binding.BindSourceType) ||
                    string.IsNullOrWhiteSpace(rule.Binding.BindTargetType))
                    throw new InvalidOperationException("Binding requires SourceType and TargetType.");

                var bindArgs = new List<ArgumentSyntax>
                {
                    SyntaxFactory.Argument(
                        SyntaxFactory.SimpleLambdaExpression(
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("sourceData")),
                            SyntaxFactory.Block(SyntaxFactory.ParseStatement(rule.Binding.BindFactory + ";"))))
                };

                if (!string.IsNullOrWhiteSpace(rule.Binding.AfterExecute))
                {
                    bindArgs.Add(SyntaxFactory.Argument(
                        SyntaxFactory.ParenthesizedLambdaExpression()
                            .WithParameterList(SyntaxFactory.ParameterList(
                                SyntaxFactory.SeparatedList([
                                    SyntaxFactory.Parameter(SyntaxFactory.Identifier("sourceData")),
                                    SyntaxFactory.Parameter(SyntaxFactory.Identifier("targetData"))
                                ])))
                            .WithBlock(SyntaxFactory.Block(
                                SyntaxFactory.ParseStatement(rule.Binding.AfterExecute)))));
                }

                baseExpression =
                    SyntaxFactory
                        .InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                baseExpression,
                                SyntaxFactory
                                    .GenericName(SyntaxFactory.Identifier("Bind"))
                                    .WithTypeArgumentList(
                                        SyntaxFactory.TypeArgumentList(
                                            SyntaxFactory.SeparatedList([
                                                SyntaxFactory.ParseTypeName(rule.Binding.BindSourceType),
                                                SyntaxFactory.ParseTypeName(rule.Binding.BindTargetType)
                                            ])))))
                        .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(bindArgs)));
            }

            // Step 3: Return final rule statement
            return SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("ruleset"),
                            SyntaxFactory.IdentifierName("AddRule")))
                    .WithArgumentList(SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(
                                SyntaxFactory.ParenthesizedLambdaExpression()
                                    .WithBlock(SyntaxFactory.Block(
                                        SyntaxFactory.ReturnStatement(baseExpression))))))));
        }
    }

    /// <summary>
    /// Generates a syntax tree for a property based on field definition.
    /// </summary>
    /// <param name="field">The field definition.</param>
    /// <returns>A property declaration syntax.</returns>
    private static PropertyDeclarationSyntax BuildPropertySyntax(FieldDefinition field)
    {
        var typeName = field.Type.Trim();
        return SyntaxFactory
            .PropertyDeclaration(
                SyntaxFactory.ParseTypeName(typeName),
                SyntaxFactory.Identifier(field.Name))
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddAccessorListAccessors(
                SyntaxFactory
                    .AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                SyntaxFactory
                    .AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            );
    }

    /// <summary>
    /// Builds a constructor syntax for a generated type.
    /// </summary>
    /// <param name="typeName">The name of the type.</param>
    /// <param name="fields">The list of fields in the type.</param>
    /// <returns>A constructor declaration syntax.</returns>
    private static ConstructorDeclarationSyntax BuildConstructorSyntax(
        string typeName,
        List<FieldDefinition> fields)
    {
        var constructor =
            SyntaxFactory
                .ConstructorDeclaration(typeName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .WithBody(
                    SyntaxFactory.Block(
                        fields
                            .Where(field => !field.Type.EndsWith("?"))
                            .Select(field => SyntaxFactory.ExpressionStatement(
                                SyntaxFactory.AssignmentExpression(
                                    SyntaxKind.SimpleAssignmentExpression,
                                    SyntaxFactory.IdentifierName(field.Name),
                                    GetDefaultValueExpression(field.Type)
                                )
                            ))
                            .ToArray<StatementSyntax>()
                    )
                );

        return constructor;
    }

    private static ExpressionSyntax GetDefaultValueExpression(string type)
    {
        var cleanType = type.TrimEnd('?');

        return cleanType switch
        {
            "string" => SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("")),
            "int" or "decimal" or "double" or "float" =>
                SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)),
            "bool" => SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression),
            "DateTime" => SyntaxFactory.ParseExpression("DateTime.MinValue"),
            _ => SyntaxFactory
                .ObjectCreationExpression(SyntaxFactory.ParseTypeName(cleanType))
                .WithArgumentList(SyntaxFactory.ArgumentList())
        };
    }
}