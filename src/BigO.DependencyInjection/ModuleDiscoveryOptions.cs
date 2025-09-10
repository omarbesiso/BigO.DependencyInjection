using System.Reflection;

namespace BigO.DependencyInjection;

/// <summary>
///     Provides configuration for module discovery performed by
///     <see
///         cref="ServiceCollectionExtensions.AddAllModules(Microsoft.Extensions.DependencyInjection.IServiceCollection, System.Action{BigO.DependencyInjection.ModuleDiscoveryOptions}?, Microsoft.Extensions.Configuration.IConfiguration?)" />
///     .
/// </summary>
/// <remarks>
///     <para>
///         By default, discovery inspects assemblies already loaded into the default AssemblyLoadContext (safe for
///         trimming/single‑file).
///     </para>
///     <para>
///         Directory probing is <b>off</b> by default. Enabling <see cref="ProbeDirectory" /> may not be supported in
///         single‑file or trimmed deployments.
///     </para>
///     <para>All predicates are optional; when provided they act as additional filters.</para>
/// </remarks>
public sealed class ModuleDiscoveryOptions
{
    /// <summary>
    ///     Filter candidate assemblies. Null = all non‑dynamic loaded assemblies.
    /// </summary>
    public Func<Assembly, bool>? AssemblyPredicate { get; set; }

    /// <summary>
    ///     Filter discovered module types. Null = all public/non‑abstract types assignable to <see cref="IModule" />.
    /// </summary>
    public Func<Type, bool>? ModulePredicate { get; set; }

    /// <summary>
    ///     Optional directory to probe (<c>*.dll</c>) and include in discovery (unsafe for single‑file/trimmed).
    /// </summary>
    public string? ProbeDirectory { get; set; }

    /// <summary>
    ///     Search option when <see cref="ProbeDirectory" /> is enabled. Default:
    ///     <see cref="SearchOption.TopDirectoryOnly" />.
    /// </summary>
    public SearchOption ProbeSearchOption { get; set; } = SearchOption.TopDirectoryOnly;

    /// <summary>Require parameterless ctor on discovered modules. Default: <see langword="true" />.</summary>
    public bool RequireParameterlessCtor { get; set; } = true;
}