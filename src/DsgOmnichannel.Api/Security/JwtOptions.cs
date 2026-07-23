namespace DsgOmnichannel.Api.Security;

internal sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "DsgOmnichannel.Local";

    public string Audience { get; set; } = "DsgOmnichannel.Api";

    public string SigningKey { get; set; } = "DsgOmnichannel.Local.Dev.Signing.Key.12345";
}
