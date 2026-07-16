using Avalonia.Media.Imaging;

namespace RivalsPorting.Models.Chat;

public record PendingGameFileAttachment(string Path, Bitmap Icon, string? DisplayName);
