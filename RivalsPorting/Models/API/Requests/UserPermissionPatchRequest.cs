using RivalsPorting.Models.Supabase.Tables;
using Newtonsoft.Json;

namespace RivalsPorting.Models.API.Requests;

public class UserPermissionPatchRequest
{
    [JsonProperty("role")] public ESupabaseRole? Role;
    [JsonProperty("canExportUEFN")] public bool? CanExportUEFN;
    [JsonProperty("isMuted")] public bool? IsMuted;
}