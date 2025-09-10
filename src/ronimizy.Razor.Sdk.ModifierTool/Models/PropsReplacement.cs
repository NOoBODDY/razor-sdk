namespace ronimizy.Razor.Sdk.ModifierTool.Models;

public class PropsReplacement
{
    public PropsReplacement(string fileName)
    {
        FileName = fileName;
    }

    public string FileName { get; init; }

    public Dictionary<string, string> Properties { get; init; } = new();
}
