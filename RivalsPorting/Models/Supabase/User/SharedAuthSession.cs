using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace RivalsPorting.Models.Supabase.User;

/// <summary>
/// Reads/writes the encrypted Discord session alongside Fortnite Porting when present.
/// Both apps use the same Supabase project and DataProtection purpose.
/// </summary>
public static class SharedAuthSession
{
    private static readonly FileInfo FortnitePortingSettingsPath = new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FortnitePorting", "AppSettingsV4.json"));

    public static UserSessionInfo? TryLoadFromFortnitePorting()
    {
        if (!FortnitePortingSettingsPath.Exists) return null;

        try
        {
            var root = JObject.Parse(File.ReadAllText(FortnitePortingSettingsPath.FullName));
            var encrypted = root["Account"]?["SessionInfoEncrypted"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(encrypted)) return null;

            return UserSessionInfo.FromEncryptedString(encrypted);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Failed to import Fortnite Porting auth session");
            return null;
        }
    }

    public static void SyncEncryptedSession(string? encryptedSession)
    {
        if (!FortnitePortingSettingsPath.Exists) return;

        try
        {
            var root = JObject.Parse(File.ReadAllText(FortnitePortingSettingsPath.FullName));
            if (root["Account"] is not JObject account)
            {
                account = new JObject();
                root["Account"] = account;
            }

            if (encryptedSession is null)
                account.Remove("SessionInfoEncrypted");
            else
                account["SessionInfoEncrypted"] = encryptedSession;

            File.WriteAllText(FortnitePortingSettingsPath.FullName, root.ToString(Formatting.Indented));
        }
        catch (Exception e)
        {
            Log.Warning(e, "Failed to sync auth session to Fortnite Porting settings");
        }
    }
}
