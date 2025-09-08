using Microsoft.Extensions.DependencyInjection;

namespace BigO.DependencyInjection;

/// <summary>
///     Represents an application module. The module class is responsible for registering the correct service definitions
///     and their implementation in each application module.
/// </summary>
/// <remarks>
///     Optional async init for modules that need it.
/// </remarks>
public interface IAsyncModule : IModule
{
    ValueTask InitializeAsync(IServiceCollection services, CancellationToken cancellationToken = default);
}