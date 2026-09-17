namespace GamePort.Cashier.Infrastructure.Security;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "GamePort.Cashier";

    public string Audience { get; set; } = "GamePort.Cashier";

    public int TokenLifetimeHours { get; set; } = 12;
}
