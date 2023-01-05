using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BigO.DependencyInjection;

/// <summary>
///     Class providing extensions to the <see cref="IServiceCollection" /> to allow for the registration of different
///     types of handlers for different message types.
/// </summary>
[PublicAPI]
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Loads a module by applying all the specified registrations and configurations to the supplied service collection.
    /// </summary>
    /// <typeparam name="TModule">The type of the module.</typeparam>
    /// <param name="serviceCollection">The service collection to contain the registrations.</param>
    /// <param name="configuration">The optional configuration instance to be used for registrations.</param>
    /// <returns>A reference to this service collection instance after the operation has completed.</returns>
    public static IServiceCollection AddModule<TModule>(this IServiceCollection serviceCollection,
        IConfiguration? configuration = null)
        where TModule : IModule, new()
    {
        var module = new TModule();
        if (configuration != null)
        {
            module.Configuration = configuration;
        }

        module.Initialize(serviceCollection);
        return serviceCollection;
    }

    /// <summary>
    ///     Scans the specified assembly for types that are assignable to the type specified by <typeparamref name="TBase" />
    ///     and adds them to the service collection.
    /// </summary>
    /// <typeparam name="TAssemblyType">The type whose assembly should be scanned.</typeparam>
    /// <typeparam name="TBase">The base type that the scanned types must be assignable to.</typeparam>
    /// <param name="serviceCollection">The service collection to which the types should be added. Cannot be <c>null</c>.</param>
    /// <param name="serviceLifetime">The lifetime of the added services. Defaults to <see cref="ServiceLifetime.Transient" />.</param>
    /// <returns>The modified service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the serviceCollection parameter is <c>null</c>.</exception>
    /// <remarks>
    ///     This method scans the assembly of the specified type <typeparamref name="TAssemblyType" /> for types that are
    ///     assignable to the type specified by <typeparamref name="TBase" />.
    ///     It then adds these types to the service collection as implemented interfaces with the specified
    ///     <paramref name="serviceLifetime" />.
    ///     If no value for <paramref name="serviceLifetime" /> is specified, the default value of
    ///     <see cref="ServiceLifetime.Transient" /> is used.
    /// </remarks>
    public static IServiceCollection AddTypesFromAssembly<TAssemblyType, TBase>(this IServiceCollection serviceCollection,
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        switch (serviceLifetime)
        {
            case ServiceLifetime.Scoped:
                serviceCollection.Scan(scan =>
                    scan.FromAssemblyOf<TAssemblyType>()
                        .AddClasses(classes => classes.AssignableTo(typeof(TBase)))
                        .AsImplementedInterfaces()
                        .WithScopedLifetime());
                break;
            case ServiceLifetime.Transient:
                serviceCollection.Scan(scan =>
                    scan.FromAssemblyOf<TAssemblyType>()
                        .AddClasses(classes => classes.AssignableTo(typeof(TBase)))
                        .AsImplementedInterfaces()
                        .WithTransientLifetime());
                break;
            case ServiceLifetime.Singleton:
                serviceCollection.Scan(scan =>
                    scan.FromAssemblyOf<TAssemblyType>()
                        .AddClasses(classes => classes.AssignableTo(typeof(TBase)))
                        .AsImplementedInterfaces()
                        .WithSingletonLifetime());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(serviceLifetime), serviceLifetime, null);
        }

        return serviceCollection;
    }
}