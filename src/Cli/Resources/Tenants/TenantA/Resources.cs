
namespace Cli.Resources.Tenants.TenantA;

public static class TenantAResources
{
    public static readonly LaunchDarklyProject MyProject =
        ProjectTemplates.ProjectA with
        {
            Key = "my-project",
            Name = "My Project"
        };


    public static readonly LaunchDarklyProject MyOtherProject =
        ProjectTemplates.ProjectA with
        {
            Key = "my-other-project",
            Name = "My Other Project",
            Environments = new()
            {
                [ProjectTemplates.DevEnvironment.Key!] = ProjectTemplates.DevEnvironment,
                [ProjectTemplates.ProdEnvironment.Key!] = ProjectTemplates.ProdEnvironment,
            }
        };

}
