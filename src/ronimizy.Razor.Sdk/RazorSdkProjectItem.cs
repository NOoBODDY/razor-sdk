using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NET.Sdk.Razor.SourceGenerators;

namespace ronimizy.Razor.Sdk;

public class RazorSdkProjectItem
{
    public RazorSdkProjectItem(AdditionalText additionalText, AnalyzerConfigOptionsProvider optionsProvider)
    {
        Options = optionsProvider.GetOptions(additionalText);

        Options.TryGetValue("build_metadata.AdditionalFiles.HintNamespace", out var hintNamespace);
        HintNamespace = hintNamespace ?? string.Empty;

        HintClassName = Path.GetFileNameWithoutExtension(additionalText.Path);

        Value = new SourceGeneratorProjectItem(
            basePath: "/",
            filePath: additionalText.Path,
            relativePhysicalPath: additionalText.Path,
            fileKind: FileKinds.GetFileKindFromPath(additionalText.Path),
            additionalText: additionalText,
            cssScope: null);
    }

    public SourceGeneratorProjectItem Value { get; }
    
    public AnalyzerConfigOptions Options { get; }

    public string HintNamespace { get; }

    public string HintClassName { get; }

    public static implicit operator SourceGeneratorProjectItem(RazorSdkProjectItem item) => item.Value;
}
