using Microsoft.AspNetCore.DataProtection;
using Newtonsoft.Json;

namespace RivalsPorting.Models.Supabase.User;

public class UserSessionInfo(string accessToken, string refreshToken)
{
    public string AccessToken { get; set; } = accessToken;
    public string RefreshToken { get; set; } = refreshToken;

    // Shared with Fortnite Porting so encrypted sessions are interchangeable.
    private static readonly IDataProtector Protector = DataProtectionProvider
        .Create("FortnitePorting")
        .CreateProtector("SessionTokens");

    // Earlier Rivals builds used a separate purpose; keep decrypting those once.
    private static readonly IDataProtector LegacyProtector = DataProtectionProvider
        .Create("RivalsPorting")
        .CreateProtector("SessionTokens");

    public string ToEncryptedString() =>
        Protector.Protect(JsonConvert.SerializeObject(this));

    public static UserSessionInfo? FromEncryptedString(string encrypted)
    {
        try
        {
            var json = Protector.Unprotect(encrypted);
            return JsonConvert.DeserializeObject<UserSessionInfo>(json);
        }
        catch
        {
            // fall through
        }

        try
        {
            var json = LegacyProtector.Unprotect(encrypted);
            return JsonConvert.DeserializeObject<UserSessionInfo>(json);
        }
        catch
        {
            return null;
        }
    }
}
