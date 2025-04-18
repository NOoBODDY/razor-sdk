// See https://aka.ms/new-console-template for more information

using Microsoft.Extensions.DependencyInjection;
using RazorSdk.ModifierTool.Commands;
using RazorSdk.ModifierTool.Infrastructure;
using Spectre.Console.Cli;

var serviceCollection = new ServiceCollection();

var app = new CommandApp(new DiTypeRegistar(serviceCollection));

app.Configure(config =>
{
    config
        .AddCommand<ModifyCommand>("modify")
        .WithDescription("Modifies the razor repository to be build as razor-sdk")
        .WithExample("modify", "./razor");
});

return await app.RunAsync(args);
