using LaunchDarkly.Terraform.Resources.Loader;

namespace Cli.Resources.Templates;

[IgnoreResource]
public class ProjectTemplates
{

    public static readonly LaunchDarklyEnvironment DevEnvironment = new()
    {
        Key = "dev",
        Name = "Development",
        Color = "0000FF",
    };

    public static readonly LaunchDarklyEnvironment TestEnvironment = new()
    {
        Key = "test",
        Name = "Test",
        Color = "00FF00",
    };

    public static readonly LaunchDarklyEnvironment ProdEnvironment = new()
    {
        Key = "prod",
        Name = "Production",
        Color = "FF0000",
        ApprovalSettings = new()
        {
            AutoApplyApprovedChanges = false,
            MinimumNumberOfApprovals = 1,
        },
        SegmentApprovalSettings = new()
        {
            AutoApplyApprovedChanges = false,
            MinimumNumberOfApprovals = 1,
        }
    };
    public static readonly LaunchDarklyProject ProjectA = new()
    {
        Key = "",
        Name = "",
        Environments = new()
        {
            [DevEnvironment.Key] = DevEnvironment,
            [TestEnvironment.Key] = TestEnvironment,
            [ProdEnvironment.Key] = ProdEnvironment,
            ["prod2"] = new()
            {
                Key = "prod2",
                Name = "Production2",
                Color = "FF0000",
                ApprovalSettings = new()
                {
                    AutoApplyApprovedChanges = false,
                    MinimumNumberOfApprovals = 2,
                    RequiredApprovalTags = ["compliance", "security"]
                },
                SegmentApprovalSettings = new()
                {
                    AutoApplyApprovedChanges = false,
                    MinimumNumberOfApprovals = 2,
                    RequiredApprovalTags = ["compliance", "security", "hxr"]
                }
            }
        }
    };

}
