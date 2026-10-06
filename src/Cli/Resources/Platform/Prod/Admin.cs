namespace Cli.Resources.Platform.Prod;

public static class Admin
{
    public static readonly LaunchDarklyCustomRole AdminRole = new()
    {
        Key = "Role-Admin",
        Name = "Admin Role",
        Description = "Role with administrative privileges",
        PolicyStatements = [
            (
                effect: Effect.Allow,
                actions: ["*"],
                resources: ["proj/*"]
            ),
            (
                effect: Effect.Allow,
                actions: ["*"],
                resources: ["team/*"]
            )
        ]
    };

    public static readonly LaunchDarklyTeam AdminTeam = new()
    {
        Key = "Team-Admin",
        Name = "Admin Team",
        Description = "Team with administrative privileges",
        CustomRoleKeys = [AdminRole.Key]
    };
}
