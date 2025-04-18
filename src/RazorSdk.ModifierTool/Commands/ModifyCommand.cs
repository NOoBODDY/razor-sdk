using System.ComponentModel;
using System.Globalization;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using RazorSdk.ModifierTool.SyntaxRewriters;
using Spectre.Console.Cli;
using Project = Microsoft.CodeAnalysis.Project;

namespace RazorSdk.ModifierTool.Commands;

public class ModifyCommand : AsyncCommand<ModifyCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[razor-directory-path]")]
        [Description("Path to directory of razor submodule")]
        public string RazorDirectoryPath { get; set; } = string.Empty;

        [CommandOption("--project-prefix")]
        [Description("Prefix that will replace some default assembly/package name prefixes")]
        public string? ProjectPrefix { get; set; }
    }

    private const string RazorSolutionName = "Razor.sln";
    private const string RazorDotnetRootPath = ".dotnet";

    private static readonly string[] ProjectToOpen = ["Microsoft.CodeAnalysis.Razor.Compiler"];

    private static readonly string[] ProjectPrefixes =
    [
        "Microsoft.CodeAnalysis.Razor",
        "Microsoft.AspNetCore.Razor",
    ];

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        string razorDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), settings.RazorDirectoryPath);
        string dotnetRootPath = Path.Combine(razorDirectoryPath, RazorDotnetRootPath);
        string dotnetSdkPath = Path.Combine(dotnetRootPath, "sdk");

        string sdkPath = Directory.EnumerateDirectories(dotnetSdkPath).First();
        string razorSolutionPath = Path.Combine(razorDirectoryPath, RazorSolutionName);

        Environment.SetEnvironmentVariable("DOTNET_ROOT", dotnetRootPath);
        Environment.SetEnvironmentVariable("PATH", $"{dotnetRootPath}:{Environment.GetEnvironmentVariable("PATH")}");
        Environment.SetEnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR", dotnetSdkPath);

        MSBuildLocator.RegisterMSBuildPath(sdkPath);

        using var workspace = MSBuildWorkspace.Create();

        await workspace.OpenSolutionAsync(razorSolutionPath);

        foreach (string projectName in ProjectToOpen)
        {
            Project project = GetProject(workspace, projectName);
            await OpenInternalsAsync(workspace, project);
            await AddNoWarnsAsync(workspace, project);
            await RemoveGeneratorsAsync(workspace, project);
        }

        foreach (string projectName in ProjectToOpen)
        {
            Project project = GetProject(workspace, projectName);
            await InlineDependenciesAsync(workspace, project, settings.ProjectPrefix);
        }

        if (string.IsNullOrEmpty(settings.ProjectPrefix) is false)
        {
            await workspace.OpenSolutionAsync(razorSolutionPath);

            foreach (string projectName in ProjectToOpen)
            {
                Project project = GetProject(workspace, projectName);
                await ModifyPrefixesAsync(workspace, project, settings.ProjectPrefix);
            }
        }

        return 0;
    }

    private Project GetProject(Workspace workspace, string projectName)
    {
        return workspace.CurrentSolution.Projects.First(project =>
            project.AssemblyName.Equals(projectName, StringComparison.OrdinalIgnoreCase)
            && project.Name.Contains("netstandard2.0"));
    }

    private async Task OpenInternalsAsync(Workspace workspace, Project project)
    {
        foreach (Document document in project.Documents)
        {
            if (document.SupportsSyntaxTree is false)
                continue;

            SyntaxTree syntaxTree = (await document.GetSyntaxTreeAsync())!;

            SyntaxNode root = await syntaxTree.GetRootAsync();
            root = new AccessModifierSyntaxRewriter().Visit(root);

            Document newDocument = document.WithSyntaxRoot(root);
            string formattedCode = root.NormalizeWhitespace(eol: Environment.NewLine).ToFullString();

            await File.WriteAllTextAsync(newDocument.FilePath!, formattedCode, Encoding.UTF8);
        }

        foreach (ProjectReference projectReference in project.ProjectReferences)
        {
            Project? referencedProject = workspace.CurrentSolution.GetProject(projectReference.ProjectId);

            if (referencedProject is not null)
                await OpenInternalsAsync(workspace, referencedProject);
        }
    }

    private async Task RemoveGeneratorsAsync(Workspace workspace, Project project)
    {
        foreach (Document document in project.Documents)
        {
            if (document.SupportsSyntaxTree is false)
                continue;

            SyntaxTree syntaxTree = (await document.GetSyntaxTreeAsync())!;
            SemanticModel semanticModel = (await document.GetSemanticModelAsync())!;

            SyntaxNode root = await syntaxTree.GetRootAsync();

            SyntaxNode[] generatorClasses = root
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(node => IsGeneratorClass(node, semanticModel))
                .Cast<SyntaxNode>()
                .ToArray();

            if (generatorClasses is [])
                continue;

            root = root.RemoveNodes(generatorClasses, SyntaxRemoveOptions.KeepNoTrivia)!;

            Document newDocument = document.WithSyntaxRoot(root);
            string formattedCode = root.NormalizeWhitespace().ToFullString();

            await File.WriteAllTextAsync(newDocument.FilePath!, formattedCode);
        }
    }

    private async Task InlineDependenciesAsync(Workspace workspace, Project project, string? targetPrefix)
    {
        var projectRoot = ProjectRootElement.Open(project.FilePath!)!;

        IEnumerable<ProjectItemElement> projectReferences = projectRoot.ItemGroups
            .SelectMany(x => x.Items)
            .Where(x => x.ElementName == "ProjectReference");

        foreach (ProjectItemElement reference in projectReferences)
        {
            reference.AddMetadata("PrivateAssets", "All");
        }

        await AddProjectReferenceDllsAsync(workspace, projectRoot.AddItemGroup(), project, targetPrefix);

        projectRoot.Save();
    }

    private async Task AddProjectReferenceDllsAsync(
        Workspace workspace,
        ProjectItemGroupElement itemGroup,
        Project project,
        string? targetPrefix)
    {
        foreach (ProjectReference projectReference in project.ProjectReferences)
        {
            Project referencedProject = workspace.CurrentSolution.GetProject(projectReference.ProjectId)!;

            TryGetAssemblyNameWithChangedPrefix(referencedProject.AssemblyName, targetPrefix, out string assemblyName);

            ProjectItemElement item = itemGroup.AddItem("None", @$"$(OutputPath)\{assemblyName}.dll");
            item.AddMetadata("Pack", "true");
            item.AddMetadata("PackagePath", @"lib\$(TargetFramework)");
            item.AddMetadata("Visible", "false");

            await AddProjectReferenceDllsAsync(workspace, itemGroup, referencedProject, targetPrefix);
        }
    }

    private bool TryGetAssemblyNameWithChangedPrefix(
        string sourceAssemblyName,
        string? targetPrefix,
        out string assemblyName)
    {
        if (string.IsNullOrEmpty(targetPrefix))
        {
            assemblyName = sourceAssemblyName;
            return false;
        }

        string? matchingPrefix = ProjectPrefixes.FirstOrDefault(x => sourceAssemblyName.StartsWith(x));

        if (matchingPrefix is null)
        {
            assemblyName = sourceAssemblyName;
            return false;
        }

        assemblyName = sourceAssemblyName.Replace(matchingPrefix, targetPrefix);
        return true;
    }

    private async Task ModifyPrefixesAsync(Workspace workspace, Project project, string targetPrefix)
    {
        var projectRoot = ProjectRootElement.Open(project.FilePath!)!;

        if (TryGetAssemblyNameWithChangedPrefix(project.AssemblyName, targetPrefix, out string assemblyName))
        {
            ProjectPropertyGroupElement propertyGroup = projectRoot.AddPropertyGroup();
            propertyGroup.AddProperty("AssemblyName", assemblyName);
            propertyGroup.AddProperty("PackageId", assemblyName);
        }

        IEnumerable<ProjectItemElement> internalsVisibleToTags = projectRoot.ItemGroups
            .SelectMany(group => group.Items)
            .Where(item => item.ElementName == "InternalsVisibleTo");

        foreach (ProjectItemElement tag in internalsVisibleToTags)
        {
            if (TryGetAssemblyNameWithChangedPrefix(tag.Include, targetPrefix, out string referenceName))
            {
                tag.Include = referenceName;
                var keyMetadata = tag.Metadata.SingleOrDefault(x => x.Name == "Key");

                if (keyMetadata is not null)
                {
                    tag.RemoveChild(keyMetadata);
                }
            }
        }

        projectRoot.Save();

        foreach (ProjectReference projectReference in project.ProjectReferences)
        {
            Project referencedProject = workspace.CurrentSolution.GetProject(projectReference.ProjectId)!;
            await ModifyPrefixesAsync(workspace, referencedProject, targetPrefix);
        }
    }

    private async Task AddNoWarnsAsync(Workspace workspace, Project project)
    {
        var projectRoot = ProjectRootElement.Open(project.FilePath!)!;

        var propertyGroup = projectRoot.AddPropertyGroup();
        propertyGroup.AddProperty("NoWarn", "$(NoWarn);RS0016;IDE0073");

        projectRoot.Save();

        foreach (ProjectReference projectReference in project.ProjectReferences)
        {
            Project referencedProject = workspace.CurrentSolution.GetProject(projectReference.ProjectId)!;
            await AddNoWarnsAsync(workspace, referencedProject);
        }
    }

    private static bool IsGeneratorClass(ClassDeclarationSyntax node, SemanticModel semanticModel)
    {
        if (semanticModel.GetDeclaredSymbol(node) is not INamedTypeSymbol symbol)
            return false;

        return symbol.GetAttributes().Any(attribute => attribute.AttributeClass?.Name == "GeneratorAttribute");
    }
}
