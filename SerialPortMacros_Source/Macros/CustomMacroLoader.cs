using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.Loader;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SerialPortMacros.Core;

namespace SerialPortMacros.Macros;

public sealed class CustomMacroException : Exception
{
    public CustomMacroException(string message) : base(message) { }
}

/// <summary>
/// Carica le macro custom da file .cs (compilati al volo con Roslyn) o .dll.
///
/// Specifica: "nomefile.cs", "nomefile.dll", oppure "nomefile.cs:NomeClasse" se il file
/// contiene più macro. I percorsi relativi partono dalla cartella "custom_macros".
///
/// Gli assembly compilati sono messi in cache finché il file non cambia; ogni macro
/// riceve comunque una propria istanza (lo stato, es. l'integrale di un PID, non è condiviso).
/// </summary>
public static class CustomMacroLoader
{
    private sealed record CacheEntry(DateTime Stamp, long Size, Assembly Assembly, AssemblyLoadContext Context);

    private static readonly object CacheLock = new();
    private static readonly Dictionary<string, CacheEntry> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lazy<List<MetadataReference>> References = new(BuildReferences);

    /// <summary>Using aggiunti automaticamente a ogni file .cs.</summary>
    private const string GlobalUsings =
        "global using System;\n" +
        "global using System.Collections.Generic;\n" +
        "global using System.Linq;\n" +
        "global using System.Text;\n" +
        "global using System.Globalization;\n" +
        "global using SerialPortMacros.Macros;\n";

    public static string ResolvePath(string spec, out string? className)
    {
        className = null;
        string file = spec.Trim();

        int colon = file.LastIndexOf(':');
        if (colon > 1)
        {
            string tail = file[(colon + 1)..];
            if (tail.Length > 0 && tail.IndexOfAny(new[] { '\\', '/' }) < 0)
            {
                className = tail.Trim();
                file = file[..colon].Trim();
            }
        }

        return Path.IsPathRooted(file) ? file : Path.Combine(AppPaths.CustomMacros, file);
    }

    /// <summary>Crea una nuova istanza della macro. Lancia <see cref="CustomMacroException"/> con il motivo dell'errore.</summary>
    public static ICustomMacro CreateInstance(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
            throw new CustomMacroException("Nessun file di codice indicato.");

        string path = ResolvePath(spec, out string? className);
        if (!File.Exists(path))
            throw new CustomMacroException($"File non trovato: {path}");

        Assembly asm = GetAssembly(path);
        Type type = FindMacroType(asm, className, path);

        try
        {
            return (ICustomMacro)Activator.CreateInstance(type)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw new CustomMacroException($"Errore nel costruttore di {type.Name}: {ex.InnerException.Message}");
        }
    }

    /// <summary>Compila il file senza istanziarlo: restituisce null se va tutto bene, altrimenti gli errori.</summary>
    public static string? Check(string spec)
    {
        try
        {
            string path = ResolvePath(spec, out string? className);
            if (!File.Exists(path)) return $"File non trovato: {path}";
            FindMacroType(GetAssembly(path), className, path);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static Assembly GetAssembly(string path)
    {
        var info = new FileInfo(path);

        lock (CacheLock)
        {
            if (Cache.TryGetValue(path, out var cached) &&
                cached.Stamp == info.LastWriteTimeUtc && cached.Size == info.Length)
                return cached.Assembly;

            var context = new MacroLoadContext(Path.GetDirectoryName(path)!);
            Assembly asm;
            try
            {
                asm = Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase)
                    ? LoadDll(context, path)
                    : Compile(context, path);
            }
            catch
            {
                context.Unload();
                throw;
            }

            if (Cache.TryGetValue(path, out var old))
                old.Context.Unload(); // la vecchia versione viene liberata quando non è più usata

            Cache[path] = new CacheEntry(info.LastWriteTimeUtc, info.Length, asm, context);
            return asm;
        }
    }

    private static Assembly LoadDll(AssemblyLoadContext context, string path)
    {
        // Caricata da memoria: il file non resta bloccato e si può ricompilare.
        using var ms = new MemoryStream(File.ReadAllBytes(path));
        return context.LoadFromStream(ms);
    }

    private static Assembly Compile(AssemblyLoadContext context, string path)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Latest);
        var trees = new[]
        {
            CSharpSyntaxTree.ParseText(File.ReadAllText(path), parse, path, Encoding.UTF8),
            CSharpSyntaxTree.ParseText(GlobalUsings, parse, "GlobalUsings.cs", Encoding.UTF8)
        };

        var options = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            optimizationLevel: OptimizationLevel.Release,
            allowUnsafe: true,
            nullableContextOptions: NullableContextOptions.Disable);

        var compilation = CSharpCompilation.Create(
            "CustomMacro_" + Path.GetFileNameWithoutExtension(path) + "_" + Guid.NewGuid().ToString("N")[..8],
            trees, References.Value, options);

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Take(15)
                .Select(d =>
                {
                    var pos = d.Location.GetLineSpan().StartLinePosition;
                    return $"  riga {pos.Line + 1}, col {pos.Character + 1}: {d.Id} {d.GetMessage()}";
                });
            throw new CustomMacroException(
                $"Errori di compilazione in {Path.GetFileName(path)}:\n" + string.Join("\n", errors));
        }

        ms.Position = 0;
        return context.LoadFromStream(ms);
    }

    private static Type FindMacroType(Assembly asm, string? className, string path)
    {
        Type[] types;
        try
        {
            types = asm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t != null).ToArray()!;
        }

        var candidates = types
            .Where(t => typeof(ICustomMacro).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface &&
                        t.GetConstructor(Type.EmptyTypes) != null)
            .ToList();

        if (className != null)
        {
            var match = candidates.FirstOrDefault(t => t.Name == className || t.FullName == className);
            return match ?? throw new CustomMacroException(
                $"La classe '{className}' non esiste in {Path.GetFileName(path)} o non implementa ICustomMacro.");
        }

        return candidates.Count switch
        {
            0 => throw new CustomMacroException(
                $"{Path.GetFileName(path)} non contiene classi pubbliche che implementano ICustomMacro " +
                "(o derivano da SimpleMacro) con un costruttore senza parametri."),
            1 => candidates[0],
            _ => throw new CustomMacroException(
                $"{Path.GetFileName(path)} contiene più macro ({string.Join(", ", candidates.Select(c => c.Name))}): " +
                "indica quale usare con \"file.cs:NomeClasse\".")
        };
    }

    // ------------------------------------------------------------------
    // Riferimenti per la compilazione.
    // Si usano i metadati degli assembly già caricati in memoria: funziona anche
    // con l'eseguibile pubblicato come "single file" (dove i .dll non esistono su disco).
    // ------------------------------------------------------------------

    private static readonly string[] ExtraAssemblies =
    {
        "System.Runtime", "netstandard", "System.Collections", "System.Collections.Concurrent",
        "System.Linq", "System.Console", "System.Text.RegularExpressions", "System.Threading",
        "System.Threading.Thread", "System.Threading.Timer", "System.Runtime.Extensions",
        "System.Memory", "System.Runtime.InteropServices", "System.ObjectModel",
        "System.Diagnostics.Debug", "System.IO.Ports", "System.Net.Primitives", "System.Net.Sockets",
        "System.Drawing.Primitives", "System.Numerics.Vectors", "System.Runtime.Numerics",
        "System.Text.Json", "System.Linq.Expressions", "System.ComponentModel.Primitives",
        "System.Diagnostics.Process", "System.IO.FileSystem"
    };

    private static List<MetadataReference> BuildReferences()
    {
        foreach (string name in ExtraAssemblies)
        {
            try { Assembly.Load(new AssemblyName(name)); } catch { }
        }

        var refs = new List<MetadataReference>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var asm in AssemblyLoadContext.Default.Assemblies)
        {
            if (asm.IsDynamic) continue;
            string? name = asm.GetName().Name;
            if (name == null || !seen.Add(name)) continue;

            var reference = CreateReference(asm);
            if (reference != null)
                refs.Add(reference);
        }

        return refs;
    }

    private static unsafe MetadataReference? CreateReference(Assembly asm)
    {
        try
        {
            if (asm.TryGetRawMetadata(out byte* blob, out int length))
            {
                var module = ModuleMetadata.CreateFromMetadata((IntPtr)blob, length);
                return AssemblyMetadata.Create(module).GetReference();
            }
        }
        catch
        {
        }

#pragma warning disable IL3000 // in single-file Location è vuoto: è solo un fallback
        try
        {
            if (!string.IsNullOrEmpty(asm.Location))
                return MetadataReference.CreateFromFile(asm.Location);
        }
        catch
        {
        }
#pragma warning restore IL3000

        return null;
    }

    /// <summary>
    /// Contesto separato (scaricabile) per ogni versione di una macro. Gli assembly già
    /// presenti nell'applicazione (incluso SerialPortMacros) sono sempre quelli condivisi,
    /// così ICustomMacro è lo stesso tipo da entrambe le parti.
    /// </summary>
    private sealed class MacroLoadContext : AssemblyLoadContext
    {
        private readonly string _directory;

        public MacroLoadContext(string directory) : base(isCollectible: true)
        {
            _directory = directory;
        }

        protected override Assembly? Load(AssemblyName name)
        {
            if (Default.Assemblies.Any(a => string.Equals(a.GetName().Name, name.Name, StringComparison.OrdinalIgnoreCase)))
                return null;

            string candidate = Path.Combine(_directory, name.Name + ".dll");
            if (File.Exists(candidate))
            {
                using var ms = new MemoryStream(File.ReadAllBytes(candidate));
                return LoadFromStream(ms);
            }

            return null; // risoluzione standard
        }
    }
}
