using Newtonsoft.Json;

namespace RivalsPorting.Models.API.Responses;

public class AuthResponse
{
    [JsonProperty("supabaseUrl")] public string SupabaseURL;
    [JsonProperty("supabaseAnonKey")] public string SupabaseAnonKey;
}