using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace SunnySunday.Cli.Infrastructure;

/// <summary>
/// Bridges Spectre.Console.Cli command resolution to Microsoft.Extensions.DependencyInjection.
/// See: https://spectreconsole.net/cli/tutorials/dependency-injection-in-cli-apps
/// </summary>
public sealed class TypeRegistrar(IServiceCollection services) : ITypeRegistrar
{
    public ITypeResolver Build() => new TypeResolver(services.BuildServiceProvider());

    [UnconditionalSuppressMessage("Trimming", "IL2067",
        Justification = "Types registered by Spectre.Console.Cli are preserved via TrimMode=partial.")]
    public void Register(Type service, Type implementation)
        => services.AddSingleton(service, implementation);

    public void RegisterInstance(Type service, object implementation)
        => services.AddSingleton(service, implementation);

    public void RegisterLazy(Type service, Func<object> factory)
        => services.AddSingleton(service, _ => factory());
}

internal sealed class TypeResolver(IServiceProvider provider) : ITypeResolver
{
    public object? Resolve(Type? type)
        => type is null ? null : provider.GetService(type);
}
