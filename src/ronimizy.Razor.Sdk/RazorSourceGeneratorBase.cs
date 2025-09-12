using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ronimizy.Razor.Sdk;

public abstract class RazorSourceGeneratorBase : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var additionalTexts = context.AdditionalTextsProvider;
        var razorFiles = additionalTexts.Where(IsApplicableRazorFile);

        var projectItems = razorFiles
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select((tuple, _) => new RazorSdkProjectItem(tuple.Left, tuple.Right));

        var importFiles = projectItems.Where(static item => item.Value.FileKind.IsComponentImport());

        var componentProjectItems = projectItems
            .Where(IsApplicableProjectItem)
            .Combine(importFiles.Collect());

        var generatedDeclarationText = componentProjectItems
            .Select((tuple, _) =>
            {
                var (projectItem, imports) = tuple;

                var fileSystem = new VirtualRazorProjectFileSystem();
                fileSystem.Add(projectItem);
                ConfigureFileSystem(fileSystem);

                Diagnostic a;

                foreach (var import in imports)
                {
                    fileSystem.Add(import);
                }

                var builder = new RazorProjectEngineBuilder(RazorConfiguration.Default, fileSystem);

                builder.Phases.Add(new DefaultRazorParsingPhase());
                builder.Phases.Add(new DefaultRazorSyntaxTreePhase());
                builder.Phases.Add(new DefaultRazorIntermediateNodeLoweringPhase());
                builder.Phases.Add(new DefaultRazorDocumentClassifierPhase());
                builder.Phases.Add(new DefaultRazorDirectiveClassifierPhase());
                builder.Phases.Add(new DefaultRazorOptimizationPhase());
                builder.Phases.Add(new DefaultRazorCSharpLoweringPhase());

                // SyntaxTreePhase
                builder.Features.Add(new DefaultDirectiveSyntaxTreePass());
                builder.Features.Add(new HtmlNodeOptimizationPass());

                // Intermediate Node Passes
                builder.Features.Add(new EliminateMethodBodyPass());

                // DocumentClassifierPhase
                builder.Features.Add(CreateClassifierPass(projectItem));

                FunctionsDirective.Register(builder);
                ImplementsDirective.Register(builder);
                InheritsDirective.Register(builder);
                NamespaceDirective.Register(builder);
                AttributeDirective.Register(builder);
                ComponentCodeDirective.Register(builder);
                ComponentInjectDirective.Register(builder);

                ConfigureEngineBuilder(builder);

                RazorProjectEngine projectEngine = builder.Build();

                RazorCodeDocument codeDocument = projectEngine.Process(projectItem);

                return (projectItem, codeDocument);
            });

        context.RegisterSourceOutput(
            generatedDeclarationText,
            (ctx, item) =>
            {
                var (projectItem, codeDocument) = item;

                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(codeDocument.GetCSharpDocument().Text);
                SyntaxNode syntaxNode = syntaxTree.GetRoot();

                syntaxNode = ModifyCSharpSyntax(syntaxNode).NormalizeWhitespace(eol: "\n");

                var shouldAddSources = OnCodeDocumentCreated(projectItem, codeDocument, syntaxNode, ctx);

                if (shouldAddSources)
                {
                    var text = syntaxNode.ToFullString();
                    var fileName = CreateFileName(projectItem);

                    ctx.AddSource(fileName, text);
                }
            });

        InitializeCore(context);
    }

    protected virtual void InitializeCore(IncrementalGeneratorInitializationContext context) { }

    protected virtual SyntaxNode ModifyCSharpSyntax(SyntaxNode csharpNode)
    {
        return csharpNode;
    }

    protected virtual string CreateFileName(RazorSdkProjectItem projectItem)
    {
        return $"{projectItem.HintNamespace}.{projectItem.HintClassName}.Razor.g.cs";
    }

    private static bool IsApplicableProjectItem(RazorSdkProjectItem file) => file.Value.FileKind.IsComponent();

    protected virtual bool IsApplicableRazorFile(AdditionalText text)
        => text.Path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase);

    protected virtual void ConfigureFileSystem(VirtualRazorProjectFileSystem fileSystem) { }

    protected virtual void ConfigureEngineBuilder(RazorProjectEngineBuilder builder) { }

    /// <summary>
    ///     Allows to analyze generated sources and decide whether they should be added to compilation
    /// </summary>
    /// <returns>Whether generated sources should be added to compilation</returns>
    protected virtual bool OnCodeDocumentCreated(
        RazorSdkProjectItem projectItem,
        RazorCodeDocument codeDocument,
        SyntaxNode syntaxNode,
        SourceProductionContext context)
    {
        return true;
    }

    protected abstract IRazorDocumentClassifierPass CreateClassifierPass(RazorSdkProjectItem projectItem);
}
