using RivalsPorting.Models.Supabase.User;
using Mapster;
using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace RivalsPorting.Models.Supabase.Tables;

[Table("levels")]
public class Levels : BaseModel
{
    [JsonProperty("xp")] public long XP;
    [JsonProperty("level")] public int Level;
}
