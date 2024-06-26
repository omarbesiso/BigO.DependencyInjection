﻿using System.Reflection;
using System.Runtime.Loader;
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
    ///     Loads a module by applying all the specified <see cref="IModule" /> registrations and configurations to the
    ///     supplied service collection.
    /// </summary>
    /// <param name="serviceCollection">The service collection to contain the registrations.</param>
    /// <param name="configuration">The optional configuration instance to be used for registrations.</param>
    /// <returns>A reference to this service collection instance after the operation has completed.</returns>
    public static IServiceCollection AddAllModules(this IServiceCollection serviceCollection,
        IConfiguration? configuration = null)
    {
        var interfaceType = typeof(IModule);

        var path = AppDomain.CurrentDomain.BaseDirectory;
        var loadedAssemblies = LoadAssembliesFromPath(path);

        var moduleTypes = loadedAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => interfaceType.IsAssignableFrom(type) && type is { IsInterface: false, IsAbstract: false })
            .ToList();

        var modules = moduleTypes.Select(moduleType => (IModule)Activator.CreateInstance(moduleType)!);

        foreach (var module in modules)
        {
            if (configuration != null)
            {
                module.Configuration = configuration;
            }

            module.Initialize(serviceCollection);
        }

        return serviceCollection;
    }

    /// <summary>
    ///     Loads assemblies from the specified path.
    /// </summary>
    /// <param name="path">The path to load assemblies from.</param>
    /// <returns>A list of loaded assemblies.</returns>
    private static List<Assembly> LoadAssembliesFromPath(string path)
    {
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();
        var loadedPaths =
            new HashSet<string>(loadedAssemblies.Select(a => a.Location), StringComparer.OrdinalIgnoreCase);
        var referencedPaths = Directory.GetFiles(path, "*.dll");
        var toLoad = referencedPaths.Where(r => !loadedPaths.Contains(r)).ToList();

        loadedAssemblies.AddRange(toLoad.Select(AssemblyName.GetAssemblyName)
            .Select(AssemblyLoadContext.Default.LoadFromAssemblyName));

        return loadedAssemblies;
    }

    /// <summary>
    ///     Adds services from an assembly containing a specified type to the <see cref="IServiceCollection" /> with the
    ///     specified <see cref="ServiceLifetime" />.
    /// </summary>
    /// <typeparam name="TAssemblyType">A type contained within the target assembly.</typeparam>
    /// <typeparam name="TBase">A base type or interface that the services should be assignable to.</typeparam>
    /// <param name="serviceCollection">The <see cref="IServiceCollection" /> to add the services to.</param>
    /// <param name="serviceLifetime">
    ///     The desired <see cref="ServiceLifetime" /> for the added services. Defaults to
    ///     <see cref="ServiceLifetime.Transient" />.
    /// </param>
    /// <returns>The updated <see cref="IServiceCollection" /> with the added services.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when an unsupported <see cref="ServiceLifetime" /> value is
    ///     passed.
    /// </exception>
    /// <example>
    ///     <code><![CDATA[
    /// IServiceCollection services = new ServiceCollection();
    /// services.AddTypesFromAssembly<Startup, IMyInterface>(ServiceLifetime.Scoped);
    /// ]]></code>
    /// </example>
    /// <remarks>
    ///     The <see cref="AddTypesFromAssembly{TAssemblyType, TBase}" /> method is a helper method that scans an assembly
    ///     containing a specified type (<typeparamref name="TAssemblyType" />) for types assignable to a specified base type
    ///     or
    ///     interface (<typeparamref name="TBase" />) and adds them to the provided <see cref="IServiceCollection" /> with the
    ///     specified <see cref="ServiceLifetime" />.
    ///     This method is useful when you want to register multiple services in a single call, rather than manually
    ///     registering each service one by one. It simplifies the process of adding services from a specific assembly that
    ///     match certain criteria, such as being assignable to a specified base type or interface.
    /// </remarks>
    public static IServiceCollection AddTypesFromAssembly<TAssemblyType, TBase>(
        this IServiceCollection serviceCollection,
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        ArgumentNullException.ThrowIfNull(serviceCollection, nameof(serviceCollection));

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