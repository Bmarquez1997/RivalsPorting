using Avalonia.Media;
using RivalsPorting.Extensions;
using RivalsPorting.Models.Supabase.Tables;

namespace RivalsPorting.Models.API.Responses;

public class UserInfoResponse
{
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public ESupabaseRole Role { get; set; }
    public bool IsMuted { get; set; }
    
    public SolidColorBrush UserBrush => Role.Brush(IsMuted);
}
