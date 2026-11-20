namespace SmartDelivery.Shared.Security;

public static class Roles
{
    public const string Customer = "Customer";
    public const string Courier = "Courier";
    public const string Seller = "Seller";
    public const string Manager = "Manager";
    public const string Admin = "Admin";
}

public class JwtSettings
{
    public const string SectionName = "JwtSettings";
    public string Secret { get; set; } = "SmartDeliverySecretKeySuperLongSecureKey2026!#$*&";
    public string Issuer { get; set; } = "SmartDeliveryAuthService";
    public string Audience { get; set; } = "SmartDeliveryServices";
    public int ExpiryMinutes { get; set; } = 120;
    public int RefreshTokenExpiryDays { get; set; } = 7;
}
