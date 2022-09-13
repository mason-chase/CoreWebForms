// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CodeDom.Compiler;
using System.Runtime.Loader;
using Microsoft.AspNetCore.SystemWebAdapters.UI.PageParser;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.SystemWebAdapters.UI.RuntimeCompilation;

internal sealed class RoslynPageCompiler : IPageCompiler
{
    private readonly ILogger<RoslynPageCompiler> _logger;
    private readonly ILoggerFactory _factory;

    public RoslynPageCompiler(ILoggerFactory factory)
    {
        _logger = factory.CreateLogger<RoslynPageCompiler>();
        _factory = factory;
    }

    public async Task<Type?> CompilePageAsync(PageFile file, CancellationToken token)
    {
        try
        {
            var (contents, className, endpointPath) = await GetSourceAsync(file.Directory, file.File).ConfigureAwait(false);

            var tree = CSharpSyntaxTree.ParseText(contents, cancellationToken: token);

            var compilation = CSharpCompilation.Create(className,
                options: new CSharpCompilationOptions(outputKind: OutputKind.DynamicallyLinkedLibrary),
                syntaxTrees: new[] { tree },
                references: GetMetadataReferences());

            var diagnostics = compilation.GetDiagnostics(token);

            if (diagnostics.Length > 0)
            {
                _logger.LogWarning("Errors found compiling {Route}", endpointPath);
                return null;
            }

            using (var ms = new MemoryStream())
            {
                compilation.Emit(ms, cancellationToken: token);
                ms.Position = 0;

                var context = new PageAssemblyLoadContext(endpointPath, _factory.CreateLogger<PageAssemblyLoadContext>());
                var assembly = context.LoadFromStream(ms);

                return assembly.GetType(className) ?? throw new InvalidOperationException("Could not find class in generated assembly");
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error compiling file {Path}", file.File.Name);
            return null;
        }
    }

    public void RemovePage(Type type)
    {
        var alc = AssemblyLoadContext.GetLoadContext(type.Assembly);

        if (alc is not PageAssemblyLoadContext)
        {
            throw new InvalidOperationException("Tried to unload something that is not a page");
        }

        alc.Unload();
    }

    private sealed class PageAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly ILogger<PageAssemblyLoadContext> _logger;
        private static long _count;

        private static string GetName(string name)
        {
            var count = Interlocked.Increment(ref _count);

            return $"{name}:{count}";
        }

        public PageAssemblyLoadContext(string route, ILogger<PageAssemblyLoadContext> logger)
            : base(GetName(route), isCollectible: true)
        {
            _logger = logger;

            logger.LogInformation("Created assembly for {Path}", Name);

            Unloading += PageAssemblyLoadContext_Unloading;
        }

        private void PageAssemblyLoadContext_Unloading(AssemblyLoadContext obj)
        {
            Unloading -= PageAssemblyLoadContext_Unloading;

            _logger.LogInformation("Unloading assembly load context for {Path}", Name);
        }
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        foreach (var assembly in AssemblyLoadContext.Default.Assemblies)
        {
            if (!assembly.IsDynamic)
            {
                yield return MetadataReference.CreateFromFile(assembly.Location);
            }
        }
    }

    private static string NormalizeName(string directory, string name)
        => Path.Combine(directory, name)
            .Replace(".", "_")
            .Replace("/", "_")
            .Replace("\\", "_");

    private static async Task<(string Contents, string ClassName, string EndpointPath)> GetSourceAsync(string directory, IFileInfo file)
    {
        using var stringWriter = new StringWriter();
        using var writer = new IndentedTextWriter(stringWriter);

        var contents = await GetContentsAsync(file).ConfigureAwait(false);
        var generator = new CSharpPageBuilder(Path.Combine(directory, file.Name), writer, contents);

        generator.WriteSource();

        return (stringWriter.ToString(), generator.ClassName, generator.Path);
    }

    private static async Task<string> GetContentsAsync(IFileInfo file)
    {
        using var stream = file.CreateReadStream();
        using var reader = new StreamReader(stream);

        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }
}