namespace BlueHeighliner.MicroGate;

/// <summary>
/// Extensions that register services by naming convention.
/// </summary>
internal static class ConventionServiceCollectionExtensions
{
    /// <summary>
    /// Registers every public interface named <c>IThing</c> in <paramref name="assembly"/> against a public, concrete, non-abstract class named <c>Thing</c> in the same namespace, if one implementing that interface exists.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="assembly">The assembly to scan for interfaces and their conventionally named implementations.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection AddConventionServices(this IServiceCollection services, Assembly assembly)
    {
        foreach (Type interfaceType in assembly.GetTypes().Where(type => type.IsInterface && type.IsPublic))
        {
            Type? implementationType = assembly.GetType($"{interfaceType.Namespace}.{interfaceType.Name[1..]}");
            if (implementationType is { IsClass: true, IsAbstract: false, IsPublic: true } && interfaceType.IsAssignableFrom(implementationType))
            {
                services.AddSingleton(interfaceType, implementationType);
            }
        }

        return services;
    }
}
