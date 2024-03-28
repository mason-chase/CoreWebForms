// MIT License.

using System.Web;
using System.Web.UI;
using Microsoft.Extensions.FileProviders;
using WebForms.Features;

namespace WebForms.Compiler.Dynamic;

public class PageCompilationOptions
{
    public PageCompilationOptions()
    {
    }

    public bool IsDebug { get; set; }

    internal List<TagNamespaceRegisterEntry> Entries { get; } = [];

    internal IFileProvider WebFormsFileProvider { get; set; } = default!;

    internal Dictionary<string, Func<VirtualPath, IWebFormsCompilationFeature, DependencyParser>> Parsers { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterPrefix(string tagPrefix, string namespaceName, string assemblyName) => Entries.Add(new(tagPrefix, namespaceName, assemblyName));

    internal void AddParser<DParser>(string extension)
        where DParser : DependencyParser, new()
    {
        DependencyParser Create(VirtualPath path, IWebFormsCompilationFeature compiledTypeAccessor)
        {
            var dependencyParser = new DParser
            {
                WebFormsFileProvider = WebFormsFileProvider,
                CompiledTypeAccessor = compiledTypeAccessor,
            };
            dependencyParser.Init(path);

            return dependencyParser;
        }

        Parsers.Add(extension, Create);
    }

}
