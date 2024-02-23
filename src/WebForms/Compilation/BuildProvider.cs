﻿//------------------------------------------------------------------------------
// <copyright file="BuildProvider.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------



/*********************************

Class hierarchy

BuildProvider
    ProfileBuildProvider
    BaseResourcesBuildProvider
        ResXBuildProvider
        ResourcesBuildProvider
    XsdBuildProvider
    WsdlBuildProvider
    InternalBuildProvider
        SourceFileBuildProvider
        SimpleHandlerBuildProvider
            WebServiceBuildProvider
            WebHandlerBuildProvider
            ImageGeneratorBuildProvider
        BaseTemplateBuildProvider
            ApplicationBuildProvider
            TemplateControlBuildProvider
                PageBuildProvider
                UserControlBuildProvider
                    MasterPageBuildProvider
            PageThemeBuildProvider
                GlobalPageThemeBuildProvider

**********************************/


using System.Reflection.Emit;

namespace System.Web.Compilation {

using System;
using System.Security.Permissions;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Web.Util;
using System.Web.UI;
using System.Web.Hosting;

// Flags returned from BuildProvider.GetResultFlags
[Flags]
public enum BuildProviderResultFlags {
    Default = 0,
    ShutdownAppDomainOnChange = 1,
}


/// <devdoc>
///    <para>
///       Base class for build providers that want to participate in a compilation.
///       It should be used by build providers that process files based on a virtual path.
///    </para>
/// </devdoc>
public abstract class BuildProvider {

    private static Dictionary<string, BuildProviderInfo> s_dynamicallyRegisteredProviders = new Dictionary<string, BuildProviderInfo>();

    //
    // Public interface
    //


    /// <devdoc>
    ///     Returns the CompilerType for the language that this build provider
    ///     needs to use, or null of it can use any language.
    /// </devdoc>
    public virtual CompilerType CodeCompilerType { get { return null; } }


    /// <devdoc>
    ///     Asks this build provider to generate any code that it needs, using the various
    ///     methods on the passed in BuildProvider.
    /// </devdoc>
    public virtual void GenerateCode(AssemblyBuilder assemblyBuilder) {}


    /// <devdoc>
    ///     Returns the class generated by this buildprovider
    /// </devdoc>
    public virtual Type GetGeneratedType(CompilerResults results) {
        return null;
    }


    /// <devdoc>
    ///     Returns a string that the provider wants to have persisted with the compiled assembly.
    /// </devdoc>
    public virtual string GetCustomString(CompilerResults results) {
        return null;
    }


    /// <devdoc>
    ///     Returns some flags that drives the behavior of the persisted result.
    /// </devdoc>
    public virtual BuildProviderResultFlags GetResultFlags(CompilerResults results) {
        return BuildProviderResultFlags.Default;
    }

    /// <devdoc>
    ///     Give the BuildProvider a chance to look at the compile errors, and possibly tweak them
    /// </devdoc>
    public virtual void ProcessCompileErrors(CompilerResults results) {
    }

    /// <devdoc>
    ///     Returns the list of files (virtual paths) that this provider depends on.
    ///     Those files need to be built before this BuildProvider.  The resulting assemblies
    ///     are added as references when compiling this BuildProvider.
    ///     This does not include things like server side includes, which don't directly
    ///     produce a BuildResult.
    ///     This is used to implement batching by separating BuildProviders
    /// </devdoc>
    internal virtual ICollection GetBuildResultVirtualPathDependencies() { return null; }

    public virtual ICollection VirtualPathDependencies {
        get {
            // By default, return the virtual path as its only dependency
            return new SingleObjectCollection(VirtualPath);
        }
    }


    /// <devdoc>
    ///     Returns the virtual path that this build provider handles
    /// </devdoc>
    protected internal string VirtualPath {
        get { return System.Web.VirtualPath.GetVirtualPathString(_virtualPath); }
    }


    /// <devdoc>
    ///     Returns the virtual path object that this build provider handles
    /// </devdoc>
    internal VirtualPath VirtualPathObject {
        get { return _virtualPath; }
    }


    /// <devdoc>
    ///     Opens a stream for the virtual file handled by this provider
    /// </devdoc>
    protected Stream OpenStream() {
        return OpenStream(VirtualPath);
    }


    /// <devdoc>
    ///     Opens a stream for a virtual file
    /// </devdoc>
    protected Stream OpenStream(string virtualPath) {
        return VirtualPathProvider.OpenFile(virtualPath);
    }

    internal /*protected*/ Stream OpenStream(VirtualPath virtualPath) {
        return virtualPath.OpenFile();
    }


    /// <devdoc>
    ///     Opens a reader for the virtual file handled by this provider
    /// </devdoc>
    protected TextReader OpenReader() {
        return OpenReader(VirtualPathObject);
    }


    /// <devdoc>
    ///     Opens a reader for a virtual file
    /// </devdoc>
    protected TextReader OpenReader(string virtualPath) {
        return OpenReader(System.Web.VirtualPath.Create(virtualPath));
    }

    internal /*protected*/ TextReader OpenReader(VirtualPath virtualPath) {
        Stream stream = OpenStream(virtualPath);
        return Util.ReaderFromStream(stream, virtualPath);
    }

    public static void RegisterBuildProvider(string extension, Type providerType) {
        if (String.IsNullOrEmpty(extension)) {
            throw ExceptionUtil.ParameterNullOrEmpty("extension");
        }
        if (providerType == null) {
            throw new ArgumentNullException("providerType");
        }
        if (!typeof(BuildProvider).IsAssignableFrom(providerType)) {
            //
            throw ExceptionUtil.ParameterInvalid("providerType");
        }
        BuildManager.ThrowIfPreAppStartNotRunning();

        // Last call wins. If a user wants to use a different provider they can always provide an
        // override in the app's config.
        s_dynamicallyRegisteredProviders[extension] = new CompilationBuildProviderInfo(providerType);
    }

    // Todo : Migration
    // internal static BuildProviderInfo GetBuildProviderInfo(System.Web.Configuration.CompilationSection config, string extension) {
    //     Debug.Assert(extension != null);
    //     var entry = config.BuildProviders[extension];
    //     if (entry != null) {
    //         return entry.BuildProviderInfo;
    //     }
    //
    //     BuildProviderInfo info = null;
    //     s_dynamicallyRegisteredProviders.TryGetValue(extension, out info);
    //     return info;
    // }


    /// <devdoc>
    ///     Returns a collection of assemblies that the build provider will be compiled with.
    /// </devdoc>
    protected ICollection ReferencedAssemblies {
        get { return _referencedAssemblies; }
    }


    // Todo : Migration
    /// <devdoc>
    ///     Returns the default CompilerType to be used for a specific language
    /// </devdoc>
    // protected CompilerType GetDefaultCompilerTypeForLanguage(string language) {
    //     return CompilationUtil.GetCompilerInfoFromLanguage(VirtualPathObject, language);
    // }


    /// <devdoc>
    ///     Returns the default CompilerType to be used for the default language
    /// </devdoc>
    protected CompilerType GetDefaultCompilerType() {
        return CompilationUtil.GetDefaultLanguageCompilerInfo(null /*compConfig*/, VirtualPathObject);
    }


    //
    // Internal code
    //

    #pragma warning disable 0649
    internal SimpleBitVector32 flags;
    #pragma warning restore 0649

    // const masks into the BitVector32
    internal const int isDependedOn                 = 0x00000001;
    internal const int noBuildResult                = 0x00000002;
    internal const int ignoreParseErrors            = 0x00000004;
    internal const int ignoreControlProperties      = 0x00000008;
    internal const int dontThrowOnFirstParseError   = 0x00000010;
    internal const int contributedCode              = 0x00000020;

    private VirtualPath _virtualPath;

    private ICollection _referencedAssemblies;

    private BuildProviderSet _buildProviderDependencies;
    internal BuildProviderSet BuildProviderDependencies {
        get {
            return _buildProviderDependencies;
        }
    }

    // Is any other BuildProvider depending on this BuildProvider
    internal bool IsDependedOn { get { return flags[isDependedOn]; } }

    internal void SetNoBuildResult() {
        flags[noBuildResult] = true;
    }

    // Remember that this BuildProvider has contributed to the compilation
    internal void SetContributedCode() {
        flags[contributedCode] = true;
    }

    internal void SetVirtualPath(VirtualPath virtualPath) {
        _virtualPath = virtualPath;
    }

    internal void SetReferencedAssemblies(ICollection referencedAssemblies) {
        _referencedAssemblies = referencedAssemblies;
    }

    internal void AddBuildProviderDependency(BuildProvider dependentBuildProvider) {
        if (_buildProviderDependencies == null)
            _buildProviderDependencies = new BuildProviderSet();

        _buildProviderDependencies.Add(dependentBuildProvider);

        dependentBuildProvider.flags[isDependedOn] = true;
    }

    /*
     * Return the culture name for this provider (e.g. "fr" or "fr-fr").
     * If no culture applies, return null.
     */
    internal string GetCultureName() {
        return Util.GetCultureName(VirtualPath);
    }

    internal BuildResult GetBuildResult(CompilerResults results) {

        BuildResult result = CreateBuildResult(results);
        if (result == null)
            return null;

        result.VirtualPath = VirtualPathObject;
        SetBuildResultDependencies(result);

        return result;
    }

    internal virtual BuildResult CreateBuildResult(CompilerResults results) {

        // If the build provider is not supposed to have a build result, just return
        if (flags[noBuildResult])
            return null;

        // Access the CompiledAssembly property to make sure the assembly gets loaded
        // Otherwise, the user code in GetGeneratedType() will fail to load it in medium trust since
        // they don't have access to the codegen folder
        if (!BuildManagerHost.InClientBuildManager && results != null) {
            // The ClientBuildManager runs in full trust, so we skip this statement
            // to avoid unnecessary loading of the assembly.
            var assembly = results.CompiledAssembly;
        }

        // Ask the build provider if it wants to persist a Type
        Type type = GetGeneratedType(results);

        BuildResult result;

        if (type != null) {
            // Create a BuildResult for it
            BuildResultCompiledType resultCompiledType = CreateBuildResult(type);

            if (!resultCompiledType.IsDelayLoadType) {
                // If the returned type doesn't come from the generated assembly, set a flag
                if (results == null || type.Assembly != results.CompiledAssembly)
                    resultCompiledType.UsesExistingAssembly = true;
            }

            result = resultCompiledType;
        }
        else {
            // Ask the build provider if it instead wants to persist a custom string
            string customString = GetCustomString(results);

            // If it does, persist it
            if (customString != null) {
                // Only preserve an assembly if the BuildProvider has actually contributed code
                // to the compilation.
                result = new BuildResultCustomString(
                    flags[contributedCode] ? results.CompiledAssembly : null,
                    customString);
            }
            else {
                // Nothing was built: nothing to persist
                if (results == null)
                    return null;

                // Otherwise, just persist the assembly, if any
                result = new BuildResultCompiledAssembly(results.CompiledAssembly);
            }
        }

        // Ask the provider it it wants to set some flags on the result
        int resultFlags = (int) GetResultFlags(results);
        if (resultFlags != 0) {
            // Make sure only the lower bits are set
            Debug.Assert((resultFlags & 0xFFFF0000) == 0);
            resultFlags &= 0x0000FFFF;

            result.Flags |= resultFlags;
        }

        return result;
    }

    internal virtual BuildResultCompiledType CreateBuildResult(Type t) {
        return new BuildResultCompiledType(t);
    }

    internal void SetBuildResultDependencies(BuildResult result) {

        result.AddVirtualPathDependencies(VirtualPathDependencies);
    }


    //
    // Helper methods
    //

    internal static CompilerType GetCompilerTypeFromBuildProvider(
        BuildProvider buildProvider) {

        HttpContext context = null;
        // Todo : Migration
        // if (EtwTrace.IsTraceEnabled(EtwTraceLevel.Verbose, EtwTraceFlags.Infrastructure) && (context = HttpContext.Current) != null)
        //     EtwTrace.Trace(EtwTraceType.ETW_TYPE_PARSE_ENTER, context.WorkerRequest);
        //
        // try {
        //     CompilerType compilerType = buildProvider.CodeCompilerType;
        //     if (compilerType != null) {
        //         CompilationUtil.CheckCompilerOptionsAllowed(compilerType.CompilerParameters.CompilerOptions,
        //             false /*config*/, null, 0);
        //     }
        //     return compilerType;
        // }
        // finally {
        //     if (EtwTrace.IsTraceEnabled(EtwTraceLevel.Verbose, EtwTraceFlags.Infrastructure) && context != null)
        //         EtwTrace.Trace(EtwTraceType.ETW_TYPE_PARSE_LEAVE, context.WorkerRequest);
        // }
        return null;
    }

    //
    // Return a 'friendly' string that identifies the build provider
    //
    internal static string GetDisplayName(BuildProvider buildProvider) {

        // If it has a VirtualPath, use it
        if (buildProvider.VirtualPath != null) {
            return buildProvider.VirtualPath;
        }

        // Otherwise, the best we can do is the type name
        return buildProvider.GetType().Name;
    }

    internal virtual ICollection GetGeneratedTypeNames() {
        return null;
    }

    #region Methods from InternalBuildProvider

    // Protected internal methods: IgnoreParseErrors and GetCodeCompileUnit

    // When this is set, we ignore parse errors and keep on processing the page as
    // well as possible.  This is used for the Venus CBM scenario
    internal virtual bool IgnoreParseErrors {
        get { return flags[ignoreParseErrors]; }
        set { flags[ignoreParseErrors] = value; }
    }

    // This is used in CBM to instruct the control builders to skip control properties
    internal bool IgnoreControlProperties {
        get { return flags[ignoreControlProperties]; }
        set { flags[ignoreControlProperties] = value; }
    }

    // Indicates whether the parser should continue processing for more errors.
    // This is used in both CBM and aspnet_compiler tool.
    internal bool ThrowOnFirstParseError {
        get { return !flags[dontThrowOnFirstParseError]; }
        set { flags[dontThrowOnFirstParseError] = !value; }
    }

    // This is used in the CBM scenario only
    internal virtual IAssemblyDependencyParser AssemblyDependencyParser {
        get { return null; }
    }

    // This is used in the CBM scenario only
    /// <devdoc>
    /// Returns the CodeCompileUnit and sets the associated dictionary containing the line pragmas.
    /// </devdoc>
    protected internal virtual CodeCompileUnit GetCodeCompileUnit(out IDictionary linePragmasTable) {

        // Default implementation with code at line 1

        string sourceString = Util.StringFromVirtualPath(VirtualPathObject);
        CodeSnippetCompileUnit snippetCompileUnit = new CodeSnippetCompileUnit(sourceString);

        LinePragmaCodeInfo codeInfo = new LinePragmaCodeInfo(1 /* startLine */, 1 /* startColumn */, 1 /* startGeneratedColumn */, -1 /* codeLength */, false /* isCodeNuggest */);
        linePragmasTable = new Hashtable();
        linePragmasTable[1] = codeInfo;

        return snippetCompileUnit;
    }

    internal virtual ICollection GetCompileWithDependencies() { return null; }

    #endregion Methods from InternalBuildProvider

    private class CompilationBuildProviderInfo : BuildProviderInfo {

        private readonly Type _type;

        public CompilationBuildProviderInfo(Type type) {
            Debug.Assert(type != null);
            _type = type;
        }

        internal override Type Type {
            get {
                return _type;
            }
        }
    }
}

// The InternalBuildProvider class is still retained to internally distinguish between
// buildProviders that actually implement the required methods and those that don't, and
// also to minimize code change where possible.
internal abstract class InternalBuildProvider: BuildProvider {

}

internal abstract class BuildProviderInfo {
    // AppliesTo value from the BuildProviderAppliesToAttribute
    private BuildProviderAppliesTo _appliesTo;

    internal abstract Type Type { get; }

    internal BuildProviderAppliesTo AppliesTo {
        get {
            if (_appliesTo != 0)
                return _appliesTo;

            // Check whether the control builder's class exposes an AppliesTo attribute
            object[] attrs = Type.GetCustomAttributes(
                typeof(BuildProviderAppliesToAttribute), /*inherit*/ true);

            if ((attrs != null) && (attrs.Length > 0)) {
                Debug.Assert(attrs[0] is BuildProviderAppliesToAttribute);
                _appliesTo = ((BuildProviderAppliesToAttribute)attrs[0]).AppliesTo;
            }
            else {
                // Default to applying to All
                _appliesTo = BuildProviderAppliesTo.All;
            }

            return _appliesTo;
        }
    }
}

}
