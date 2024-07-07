
# BigO.DependencyInjection

[![NuGet version](https://badge.fury.io/nu/BigO.DependencyInjection.svg)](https://badge.fury.io/nu/BigO.DependencyInjection)

BigO.DependencyInjection provides a set of utilities to streamline and enhance dependency injection in .NET projects. This package aims to simplify common dependency injection patterns, making your code more maintainable and testable.

## Features

- **Service Registration**: Easily register services with various lifetimes (singleton, scoped, transient).
- **Decorator Support**: Apply decorators to services seamlessly.
- **Factory Registration**: Register services with factory methods.

## Installation

Install via NuGet Package Manager Console:

```bash
Install-Package BigO.DependencyInjection
```

Or via .NET CLI:

```bash
dotnet add package BigO.DependencyInjection
```

## Usage

### Registering Services

```csharp
using BigO.DependencyInjection;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddBigOService<MyService>(ServiceLifetime.Singleton);
    }
}
```

### Applying Decorators

```csharp
services.AddDecoratedService<IMyService, MyService, MyServiceDecorator>(ServiceLifetime.Scoped);
```

### Registering with Factory

```csharp
services.AddFactoryService<IMyService>(sp => new MyService(sp.GetRequiredService<IOtherService>()));
```

## Contributing

Contributions are welcome! Please open an issue or submit a pull request for any bugs or features.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
