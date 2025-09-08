using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scrutor;

// ReSharper disable UnusedMember.Global

namespace BigO.DependencyInjection;

/// <summary>
///     Extension methods for <see cref="IServiceCollection" /> that enable module-based registration
///     and assembly scanning helpers.
/// </summary>
/// <remarks>
///     <para>
///         The discovery pipeline is reflection-based. The default path scans only assemblies already loaded into
///         the default <see cref="AssemblyLoadContext" />, which is friendlier to trimming and single-file publish.
///     </para>
///     <para>
///         Directory probing (loading <c>*.dll</c> from disk) is <b>opt-in</b> and annotated with
///         <see cref="RequiresAssemblyFilesAttribute" /> and <see cref="RequiresUnreferencedCodeAttribute" />.
///     </para>
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     v2.x binary-compat: original signature (no CancellationToken). Forwards to the new overload.
    /// </summary>
    public static IServiceCollection AddModule<TModule>(
        this IServiceCollection services,
        IConfiguration? configuration = null)
        where TModule : IModule, new()
        => AddModule<TModule>(services, configuration, CancellationToken.None);

    /// <summary>
    ///     v2.x binary/behavioral-compat: original signature that *probed base directory by default*.
    /// </summary>
    public static IServiceCollection AddAllModules(
        this IServiceCollection services,
        IConfiguration? configuration = null)
        => AddAllModules(
            services,
            configuration,
            options =>
            {
                // Preserve 2.x behavior: load *.dll from base directory + already-loaded assemblies.
                options.ProbeDirectory = AppContext.BaseDirectory;
                options.ProbeSearchOption = SearchOption.TopDirectoryOnly;
            },
            CancellationToken.None);


    /// <summary>
    ///     Instantiates and initializes a module of type <typeparamref name="TModule" />.
    /// </summary>
    /// <typeparam name="TModule">A type that implements <see cref="IModule" /> and exposes a parameterless constructor.</typeparam>
    /// <param name="services">The DI service collection to register services into.</param>
    /// <param name="configuration">
    ///     Optional configuration instance to assign to <see cref="IModule.Configuration" /> prior to initialization.
    /// </param>
    /// <param name="cancellationToken">
    ///     A token that can be observed while awaiting asynchronous initialization for modules implementing
    ///     <see cref="IAsyncModule" />.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection" /> instance to enable fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services" /> is <see langword="null" />.</exception>
    /// <remarks>
    ///     <para>
    ///         If <typeparamref name="TModule" /> implements <see cref="IAsyncModule" />, its
    ///         <see
    ///             cref="IAsyncModule.InitializeAsync(Microsoft.Extensions.DependencyInjection.IServiceCollection, System.Threading.CancellationToken)" />
    ///         is invoked; otherwise
    ///         <see cref="IModule.Initialize(Microsoft.Extensions.DependencyInjection.IServiceCollection)" /> is used.
    ///     </para>
    ///     <para>
    ///         Call these methods <b>before</b> building the service provider.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code><![CDATA[
    /// services.AddModule<MyInfrastructureModule>(configuration, cancellationToken);
    /// ]]></code>
    /// </example>
    public static IServiceCollection AddModule<TModule>(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
        where TModule : IModule, new()
    {
        ArgumentNullException.ThrowIfNull(services);

        var module = new TModule();
        if (configuration is not null)
        {
            module.Configuration = configuration;
        }

        if (module is IAsyncModule asyncModule)
        {
            asyncModule.InitializeAsync(services, cancellationToken).GetAwaiter().GetResult();
        }
        else
        {
            module.Initialize(services);
        }

        return services;
    }

    /// <summary>
    ///     Discovers and initializes all public, non-abstract types assignable to <see cref="IModule" />
    ///     from candidate assemblies, then invokes each module's initialization routine.
    /// </summary>
    /// <param name="services">The DI service collection to register services into.</param>
    /// <param name="configuration">
    ///     Optional configuration instance to assign to each module via <see cref="IModule.Configuration" />.
    /// </param>
    /// <param name="configure">
    ///     Optional delegate to configure <see cref="ModuleDiscoveryOptions" /> for filtering and directory probing.
    /// </param>
    /// <param name="cancellationToken">
    ///     A token observed when invoking asynchronous initialization for modules implementing <see cref="IAsyncModule" />.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection" /> instance to enable fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services" /> is <see langword="null" />.</exception>
    /// <remarks>
    ///     <para>
    ///         By default, only assemblies already loaded into the default <see cref="AssemblyLoadContext" /> are scanned.
    ///         This avoids common pitfalls with trimming and single-file publish.
    ///     </para>
    ///     <para>
    ///         Enabling directory probing via <see cref="ModuleDiscoveryOptions.ProbeDirectory" /> will load additional
    ///         assemblies
    ///         from disk and is not recommended for single-file or trimmed deployments.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code><![CDATA[
    /// // Safe default discovery:
    /// services.AddAllModules(configuration, options =>
    /// {
    ///     options.AssemblyPredicate = asm => asm.GetName().Name!.StartsWith("MyCompany.", StringComparison.Ordinal);
    ///     options.ModulePredicate   = type => type.Namespace is not null && type.Namespace.EndsWith(".Modules", StringComparison.Ordinal);
    /// });
    /// 
    /// // Opt-in probing (unsafe for single-file):
    /// services.AddAllModules(configuration, options =>
    /// {
    ///     options.ProbeDirectory = AppContext.BaseDirectory;
    ///     options.ProbeSearchOption = SearchOption.TopDirectoryOnly;
    /// });
    /// ]]></code>
    /// </example>
    [RequiresUnreferencedCode(
        "Reflection-based module discovery is not trimming-safe. Prefer explicit registration or pass assemblies explicitly.")]
    public static IServiceCollection AddAllModules(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        Action<ModuleDiscoveryOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new ModuleDiscoveryOptions();
        configure?.Invoke(options);

        var assemblies = GetCandidateAssemblies(options);
        var modules = GetModuleTypes(assemblies, options);

        foreach (var type in modules)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var module = CreateModuleInstance(type, options.RequireParameterlessCtor);
            if (module is null)
            {
                continue;
            }

            if (configuration is not null)
            {
                module.Configuration = configuration;
            }

            if (module is IAsyncModule asyncModule)
            {
                asyncModule.InitializeAsync(services, cancellationToken).GetAwaiter().GetResult();
            }
            else
            {
                module.Initialize(services);
            }
        }

        return services;
    }

    /// <summary>
    ///     Registers all public classes in the assembly containing <typeparamref name="TAssemblyType" />
    ///     that are assignable to <typeparamref name="TBase" /> using the specified <see cref="ServiceLifetime" />.
    /// </summary>
    /// <typeparam name="TAssemblyType">A marker type used to locate the target assembly for scanning.</typeparam>
    /// <typeparam name="TBase">A base class or interface used to filter candidate classes.</typeparam>
    /// <param name="services">The DI service collection to add registrations to.</param>
    /// <param name="lifetime">
    ///     The desired lifetime for registrations. Defaults to <see cref="ServiceLifetime.Transient" />.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection" /> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services" /> is <see langword="null" />.</exception>
    /// <remarks>
    ///     <para>
    ///         Uses <b>Scrutor</b> to scan the assembly and registers discovered types as their implemented interfaces.
    ///         Duplicate registrations are skipped via <see cref="RegistrationStrategy.Skip" />.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code><![CDATA[
    /// services.AddTypesFromAssembly<MyApiMarker, IMyService>(ServiceLifetime.Scoped);
    /// ]]></code>
    /// </example>
    public static IServiceCollection AddTypesFromAssembly<TAssemblyType, TBase>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Scan(scan => scan
            .FromAssemblyOf<TAssemblyType>()
            .AddClasses(c => c.AssignableTo(typeof(TBase)))
            .UsingRegistrationStrategy(RegistrationStrategy.Skip)
            .AsImplementedInterfaces()
            .WithLifetime(_ => lifetime));

        return services;
    }

    /// <summary>
    ///     Returns the set of candidate assemblies to inspect based on <paramref name="options" />.
    /// </summary>
    private static IEnumerable<Assembly> GetCandidateAssemblies(ModuleDiscoveryOptions options)
    {
        var loaded = AssemblyLoadContext.Default.Assemblies;

        var filtered = options.AssemblyPredicate is null
            ? loaded.Where(a => !a.IsDynamic)
            : loaded.Where(a => !a.IsDynamic && options.AssemblyPredicate(a));

        if (!string.IsNullOrWhiteSpace(options.ProbeDirectory))
        {
            foreach (var a in LoadAssembliesFromPath(options.ProbeDirectory!, options.ProbeSearchOption))
            {
                if (options.AssemblyPredicate is null || options.AssemblyPredicate(a))
                {
                    filtered = filtered.Append(a);
                }
            }
        }

        return filtered.DistinctBy(a => a.FullName);
    }

    /// <summary>
    ///     Retrieves public, non-abstract types assignable to <see cref="IModule" /> from the supplied assemblies.
    ///     Applies the optional <see cref="ModuleDiscoveryOptions.ModulePredicate" />.
    /// </summary>
    private static IEnumerable<Type> GetModuleTypes(IEnumerable<Assembly> assemblies, ModuleDiscoveryOptions options)
    {
        var moduleType = typeof(IModule);

        foreach (var asm in assemblies)
        {
            Type[] types;
            try
            {
                types = asm.GetExportedTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // ReSharper disable once RedundantSuppressNullableWarningExpression
                types = ex.Types.Where(t => t is not null)!.Cast<Type>().ToArray();
            }
            catch
            {
                continue;
            }

            foreach (var t in types)
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                if (t is null)
                {
                    continue;
                }

                if (!moduleType.IsAssignableFrom(t))
                {
                    continue;
                }

                if (t.IsInterface || t.IsAbstract)
                {
                    continue;
                }

                if (options.ModulePredicate is not null && !options.ModulePredicate(t))
                {
                    continue;
                }

                yield return t;
            }
        }
    }

    /// <summary>
    ///     Creates an instance of the specified module type, honoring the parameterless constructor requirement.
    /// </summary>
    private static IModule? CreateModuleInstance(Type moduleType, bool requireParameterlessCtor)
    {
        try
        {
            if (requireParameterlessCtor && moduleType.GetConstructor(Type.EmptyTypes) is null)
            {
                return null;
            }

            return (IModule?)Activator.CreateInstance(moduleType, true);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Loads assemblies from the specified directory. Not supported in single-file bundles and not trimming-safe.
    /// </summary>
    [RequiresAssemblyFiles("Loads assemblies from disk; not supported for single-file bundles.")]
    [RequiresUnreferencedCode("Loading arbitrary assemblies is not trimming-safe.")]
    private static IEnumerable<Assembly> LoadAssembliesFromPath(string path, SearchOption searchOption)
    {
        if (!Directory.Exists(path))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(path, "*.dll", searchOption))
        {
            Assembly? asm;
            try
            {
                asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(file));
            }
            catch
            {
                asm = null;
            }

            if (asm is not null)
            {
                yield return asm;
            }
        }
    }
}