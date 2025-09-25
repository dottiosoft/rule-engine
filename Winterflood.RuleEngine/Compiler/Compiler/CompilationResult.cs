using System.Reflection;

namespace Winterflood.RuleEngine.Compiler.Compiler;

/// <summary>
/// 
/// </summary>
/// <param name="type"></param>
/// <param name="success"></param>
/// <param name="message"></param>
/// <param name="meta"></param>
public class CompilationUnitResult(
    string type,
    bool success,
    string message,
    string meta)
{
    public string Type { get; } = type;
    public bool Success { get; } = success;
    public string Message { get; } = message;
    public string Meta { get; } = meta;
}

/// <summary>
/// 
/// </summary>
/// <param name="unitResults"></param>
/// <param name="compiledAssembly"></param>
public class CompilationResult(
    List<CompilationUnitResult> unitResults,
    Assembly? compiledAssembly)
{
    public bool Success => UnitResults.All(r => r.Success);
    public List<CompilationUnitResult> UnitResults { get; } = unitResults;
    public Assembly? CompiledAssembly { get; } = compiledAssembly;
}