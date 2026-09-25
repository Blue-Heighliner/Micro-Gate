namespace BlueHeighliner.MicroGate;

/// <summary>
/// Extensions that register services by naming convention.
/// </summary>
internal static class ConventionServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers as transient every public interface named <c>IThing</c> in <paramref name="assembly"/> against a public, concrete, non-abstract class named <c>Thing</c> in the same namespace, if one implementing that interface exists.
        /// </summary>
        /// <param name="assembly">The assembly to scan for interfaces and their conventionally named implementations.</param>
        /// <returns>The service collection, for chaining.</returns>
        public IServiceCollection AddConventionServices(Assembly assembly)
        {
            foreach (Type interfaceType in assembly.GetTypes().Where(type => type.IsInterface && type.IsPublic))
            {
                Type? implementationType = assembly.GetType($"{interfaceType.Namespace}.{interfaceType.Name[1..]}");
                if (implementationType is { IsClass: true, IsAbstract: false, IsPublic: true } && interfaceType.IsAssignableFrom(implementationType))
                {
                    services.AddTransient(interfaceType, implementationType);
                }
            }

            return services;
        }
    }
}
