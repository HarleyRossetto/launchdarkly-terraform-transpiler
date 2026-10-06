namespace Cli.Resources.Platform.Prod;

public class Relay
{
    public static readonly RelayProxyConfiguration LdRelayProd = new()
    {
        Name = "prod-relay",
        Policy = [
            (
             effect: Effect.Allow,
             actions: ["*"],
             resources: ["proj/*:env/*"]
            )
        ]
    };
}
