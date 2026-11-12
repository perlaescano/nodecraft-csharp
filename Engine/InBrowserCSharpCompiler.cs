using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace NodeCraftCSharp.Engine;

public enum ExecutionMode
{
    None,
    GameScene,
    ConsoleProgram
}

public record CompileResult(
    bool Success,
    ExecutionMode Mode,
    Node? EntryScene,
    List<string> Diagnostics
);

public class CollectibleScriptContext : AssemblyLoadContext
{
    public CollectibleScriptContext() : base(isCollectible: true) { }
    protected override Assembly? Load(AssemblyName assemblyName) => null;
}

public class InBrowserCSharpCompiler
{
    private readonly List<MetadataReference> _baseReferences;
    private readonly List<string> _loadedLibraries = new();
    private CollectibleScriptContext? _currentContext;
    private bool _engineRefLoaded;

    public IReadOnlyList<string> LoadedLibraries => _loadedLibraries;

    public InBrowserCSharpCompiler()
    {
        // Full .NET 8 Base Class Library (System, Collections, Linq, Text.Json, Math, etc.)
        _baseReferences = Net80.References.All.ToList<MetadataReference>();
        _loadedLibraries.Add(".NET 8 Standard Library (System, LINQ, JSON, Math)");
    }

    /// <summary>
    /// Loads the Engine assembly metadata from wwwroot/engine-ref.dat so student code can inherit from Node & Node2D.
    /// </summary>
    public async Task InitializeAsync(HttpClient http)
    {
        if (_engineRefLoaded) return;

        byte[] engineDllBytes = await http.GetByteArrayAsync("engine-ref.dat");
        if (engineDllBytes.Length == 0)
            throw new InvalidOperationException("engine-ref.dat is empty. Run 'dotnet build' to generate engine metadata.");

        _baseReferences.Add(MetadataReference.CreateFromImage(engineDllBytes));
        _loadedLibraries.Add("NodeCraftCSharp.Engine (Node, Node2D, Sprite2D, Label, Vector2, Input, Audio)");
        _engineRefLoaded = true;
    }

    /// <summary>
    /// Allows teachers or students to load any pure-managed .NET library (.dll) into the in-browser compiler.
    /// </summary>
    public void AddExternalLibrary(string libraryName, byte[] dllBytes)
    {
        _baseReferences.Add(MetadataReference.CreateFromImage(dllBytes));
        if (!_loadedLibraries.Contains(libraryName))
            _loadedLibraries.Add(libraryName);
    }

    /// <summary>
    /// Compiles and runs either a 2D Game Scene (Node/Node2D) OR a standalone C# Console Program (Main / top-level statements).
    /// </summary>
    public CompileResult CompileAndRun(string studentSourceCode)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(studentSourceCode);

        // First try compiling as a Library (supports Node/Node2D game classes AND explicit Program.Main classes)
        var dllCompilation = CSharpCompilation.Create(
            assemblyName: $"StudentCode_{Guid.NewGuid():N}",
            syntaxTrees: new[] { syntaxTree },
            references: _baseReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        using var ms = new MemoryStream();
        var emitResult = dllCompilation.Emit(ms);

        // If student wrote C# Top-Level Statements (e.g. just Console.WriteLine("Hi");), Roslyn emits CS0116 in DLL mode.
        // Automatically retry as ConsoleApplication!
        if (!emitResult.Success && emitResult.Diagnostics.Any(d => d.Id == "CS0116"))
        {
            var exeCompilation = CSharpCompilation.Create(
                assemblyName: $"StudentConsole_{Guid.NewGuid():N}",
                syntaxTrees: new[] { syntaxTree },
                references: _baseReferences,
                options: new CSharpCompilationOptions(OutputKind.ConsoleApplication)
            );

            using var exeMs = new MemoryStream();
            var exeEmit = exeCompilation.Emit(exeMs);

            if (!exeEmit.Success)
                return new CompileResult(false, ExecutionMode.None, null, FormatErrors(exeEmit.Diagnostics));

            return LoadAndExecuteConsoleAssembly(exeMs);
        }

        if (!emitResult.Success)
            return new CompileResult(false, ExecutionMode.None, null, FormatErrors(emitResult.Diagnostics));

        // Unload previous compiled student assembly so browser memory stays clean
        _currentContext?.Unload();
        _currentContext = new CollectibleScriptContext();

        ms.Seek(0, SeekOrigin.Begin);
        Assembly studentAssembly = _currentContext.LoadFromStream(ms);

        // 1. Check if the code defines a 2D Game Scene (a class inheriting from Node)
        var sceneCandidates = studentAssembly.GetTypes()
            .Where(t => typeof(Node).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
            .ToList();

        if (sceneCandidates.Count > 0)
        {
            Type sceneType = sceneCandidates.FirstOrDefault(t =>
                                 t.Name.Contains("Game", StringComparison.OrdinalIgnoreCase) ||
                                 t.Name.Contains("Main", StringComparison.OrdinalIgnoreCase) ||
                                 t.Name.Contains("World", StringComparison.OrdinalIgnoreCase) ||
                                 t.Name.Contains("Demo", StringComparison.OrdinalIgnoreCase))
                             ?? sceneCandidates.Last();

            try
            {
                var instance = (Node?)Activator.CreateInstance(sceneType);
                return new CompileResult(true, ExecutionMode.GameScene, instance, new List<string>());
            }
            catch (Exception ex)
            {
                return new CompileResult(false, ExecutionMode.None, null, new List<string>
                {
                    $"Could not start scene '{sceneType.Name}': {ex.InnerException?.Message ?? ex.Message}"
                });
            }
        }

        // 2. Otherwise, check if the code defines a standalone C# static Main() method
        MethodInfo? mainMethod = studentAssembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .FirstOrDefault(m => m.Name == "Main");

        if (mainMethod != null)
        {
            return InvokeMainMethod(mainMethod);
        }

        return new CompileResult(false, ExecutionMode.None, null, new List<string>
        {
            "No runnable entry point found! Either write C# statements / a 'Main()' method to print to the console, or create a class inheriting from 'Node2D'."
        });
    }

    private CompileResult LoadAndExecuteConsoleAssembly(MemoryStream ms)
    {
        _currentContext?.Unload();
        _currentContext = new CollectibleScriptContext();

        ms.Seek(0, SeekOrigin.Begin);
        Assembly studentAssembly = _currentContext.LoadFromStream(ms);

        if (studentAssembly.EntryPoint == null)
        {
            return new CompileResult(false, ExecutionMode.None, null, new List<string>
            {
                "No Main() entry point found."
            });
        }

        return InvokeMainMethod(studentAssembly.EntryPoint);
    }

    private static CompileResult InvokeMainMethod(MethodInfo mainMethod)
    {
        try
        {
            var parameters = mainMethod.GetParameters();
            object?[]? args = parameters.Length == 1 && parameters[0].ParameterType == typeof(string[])
                ? new object?[] { Array.Empty<string>() }
                : parameters.Length == 0
                    ? null
                    : throw new InvalidOperationException("Main() must have either () or (string[] args) parameters.");

            object? result = mainMethod.Invoke(null, args);
            if (result is Task task)
                task.GetAwaiter().GetResult();

            return new CompileResult(true, ExecutionMode.ConsoleProgram, null, new List<string>());
        }
        catch (Exception ex)
        {
            return new CompileResult(false, ExecutionMode.ConsoleProgram, null, new List<string>
            {
                $"Runtime Error in Main(): {ex.InnerException?.Message ?? ex.Message}"
            });
        }
    }

    private static List<string> FormatErrors(IEnumerable<Diagnostic> diagnostics)
    {
        return diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => $"Line {d.Location.GetLineSpan().StartLinePosition.Line + 1}: {d.GetMessage()}")
            .ToList();
    }
}
