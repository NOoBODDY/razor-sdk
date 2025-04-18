using Spectre.Console.Cli;

namespace RazorSdk.ModifierTool.Infrastructure;

public class DiTypeResolver : ITypeResolver, IDisposable
{
    private readonly IServiceProvider _provider;

    public DiTypeResolver(IServiceProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public object? Resolve(Type? type)
    {
        if (type == null)
        {
            return null;
        }

        return _provider.GetService(type);
    }

    public void Dispose()
    {
        if (_provider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
