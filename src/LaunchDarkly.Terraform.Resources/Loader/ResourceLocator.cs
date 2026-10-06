using System.Reflection;

namespace LaunchDarkly.Terraform.Resources.Loader;

public class ResourceLocator
{
    private const BindingFlags StaticFieldFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static IEnumerable<Type> GetAllResourceTypes()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceTypes = assembly.GetTypes()
            .Where(t => typeof(ITerraformResource).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .Where(t => t.GetCustomAttribute<IgnoreResourceAttribute>() == null);

        return resourceTypes;
    }

    public static IEnumerable<ScopedResources> ScopeResources(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var scopedHolders = assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<IgnoreResourceAttribute>() is null)
            .Where(type => type
                .GetFields(StaticFieldFlags)
                .Any(field => field.GetCustomAttribute<IgnoreResourceAttribute>() is null
                    && typeof(ITerraformResource).IsAssignableFrom(field.FieldType)))
            .Select(type =>
            {
                var fields = type.GetFields(StaticFieldFlags)
                    .Where(field => field.GetCustomAttribute<IgnoreResourceAttribute>() is null)
                    .ToArray();
                var resourceFields = fields
                    .Where(field => typeof(ITerraformResource).IsAssignableFrom(field.FieldType))
                    .ToArray();
                var configurationFields = fields
                    .Where(field => typeof(TerraformConfiguration).IsAssignableFrom(field.FieldType))
                    .ToArray();

                if (configurationFields.Length > 1)
                {
                    throw new InvalidOperationException(
                        $"Resource holder '{type.FullName}' declares more than one Terraform configuration override.");
                }

                return new
                {
                    Scope = type.Namespace
                        ?? throw new InvalidOperationException(
                            $"Resource holder '{type.FullName}' does not have a namespace to use as its scope."),
                    Resources = resourceFields
                        .Select(field => field.GetValue(null) as ITerraformResource)
                        .Where(resource => resource is not null)
                        .Cast<ITerraformResource>()
                        .ToArray(),
                    ProviderConfiguration = configurationFields
                        .Select(field => field.GetValue(null) as TerraformConfiguration)
                        .SingleOrDefault()
                };
            })
            .Where(holder => holder.Resources.Length > 0)
            .GroupBy(holder => holder.Scope);

        return scopedHolders
            .Select(group =>
            {
                var overrides = group
                    .Select(holder => holder.ProviderConfiguration)
                    .Where(configuration => configuration is not null)
                    .Cast<TerraformConfiguration>()
                    .ToArray();

                if (overrides.Length > 1)
                {
                    throw new InvalidOperationException(
                        $"Scope '{group.Key}' has multiple Terraform configuration overrides. Declare at most one override per scope.");
                }

                return new ScopedResources
                {
                    Scope = group.Key,
                    Resources = group.SelectMany(holder => holder.Resources),
                    ProviderConfiguration = overrides.SingleOrDefault()
                };
            });
    }


}

/**
 * A scoped resoruce is meant to pull the namespace and keep track of that.
 * The intention is to later allow this tool to create multiple different states to be managed,
 * potentially using different terraform versions etc.
 * Because of how the launchdarkly terraform provider works, we might need to use this tool as a
 * entery-point to calling terraform commands in the right order.
 * This scope will be used to output the json files do a directory which matches the scope name,
 * terraform can then be run from that directory.
 */
public record ScopedResources
{
    public required string Scope { get; init; }
    public required IEnumerable<ITerraformResource> Resources { get; init; }
    public TerraformConfiguration? ProviderConfiguration { get; init; }
}
