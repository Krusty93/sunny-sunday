using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace SunnySunday.Cli.Infrastructure;

/// <summary>
/// Bridges Spectre.Console.Cli command resolution to Microsoft.Extensions.DependencyInjection.
/// See: https://spectreconsole.net/cli/tutorials/dependency-injection-in-cli-apps
/// </summary>
public sealed class TypeRegistrar(IServiceProvider services) : ITypeRegistrar
{
    private readonly Dictionary<Type, Type> registrations = [];
    private readonly Dictionary<Type, object> instances = [];
    private readonly Dictionary<Type, Func<object>> factories = [];

    public ITypeResolver Build() => new TypeResolver(services, registrations, instances, factories);

    [UnconditionalSuppressMessage("Trimming", "IL2067",
        Justification = "Types registered by Spectre.Console.Cli are preserved via TrimMode=partial.")]
    public void Register(Type service, Type implementation)
        => registrations[service] = implementation;

    public void RegisterInstance(Type service, object implementation)
        => instances[service] = implementation;

    public void RegisterLazy(Type service, Func<object> factory)
        => factories[service] = factory;
}

internal sealed class TypeResolver(
    IServiceProvider provider,
    IReadOnlyDictionary<Type, Type> registrations,
    IReadOnlyDictionary<Type, object> instances,
    IReadOnlyDictionary<Type, Func<object>> factories) : ITypeResolver
{
    public object? Resolve(Type? type)
    {
        if (type is null)
        {
            return null;
        }

        if (instances.TryGetValue(type, out var implementation))
        {
            return implementation;
        }

        if (factories.TryGetValue(type, out var factory))
        {
            return factory();
        }

        if (registrations.TryGetValue(type, out var registeredType))
        {
            return ActivatorUtilities.GetServiceOrCreateInstance(provider, registeredType);
        }

        return provider.GetService(type);
    }
}
