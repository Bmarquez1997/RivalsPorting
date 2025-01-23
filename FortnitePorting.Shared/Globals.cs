using System.Collections.ObjectModel;
using Avalonia.Platform.Storage;
using CUE4Parse.UE4.Objects.Core.Misc;
using FortnitePorting.Shared.Models;

namespace FortnitePorting.Shared;

public static class Globals
{
    public static string VersionString => Version.GetDisplayString(EVersionStringType.IdentifierPrefix);
    public static readonly FPVersion Version = new(0, 1, 4, identifier: "Alpha");
    public const string OnlineTag = "RivalsPorting";
    
    public static readonly FilePickerFileType MappingsFileType = new("Unreal Mappings") { Patterns = [ "*.usmap" ] };
    public static readonly FilePickerFileType JSONFileType = new("JSON") { Patterns = [ "*.json" ] };
    public static readonly FilePickerFileType MP3FileType = new("MP3 Audio") { Patterns = [ "*.mp3" ] };
    public static readonly FilePickerFileType WAVFileType = new("WAV Audio") { Patterns = [ "*.wav" ] };
    public static readonly FilePickerFileType OGGFileType = new("OGG Audio") { Patterns = [ "*.ogg" ] };
    public static readonly FilePickerFileType FLACFileType = new("FLAC Audio") { Patterns = [ "*.flac" ] };
    public static readonly FilePickerFileType PNGFileType = new("PNG Image") { Patterns = [ "*.png" ] };
    public static readonly FilePickerFileType GIFFileType = new("GIF Image") { Patterns = [ "*.gif" ] };
    public static readonly FilePickerFileType ImageFileType = new("Image") { Patterns = [ "*.png", "*.jpg", "*.jpeg", "*.tga" ] };
    public static readonly FilePickerFileType PlaylistFileType = new("Fortnite Porting Playlist") { Patterns = [ "*.fp.playlist" ] };
    public static readonly FilePickerFileType ChatAttachmentFileType = new("Image") { Patterns = [ "*.png", "*.jpg", "*.jpeg" ] };
    public static readonly FilePickerFileType BlenderFileType = new("Blender") { Patterns = ["blender.exe"] };
    public static readonly FilePickerFileType UnrealProjectFileType = new("Unreal Project") { Patterns = ["*.uproject"] };
    
    public static readonly FGuid ZERO_GUID = new();
    public const string ZERO_CHAR = "0x0000000000000000000000000000000000000000000000000000000000000000";
    
    public const string LATEST_AES = "0x0C263D8C22DCB085894899C3A3796383E9BF9DE0CBFB08C9BF2DEF2E84F29D74";
    
    public static readonly ReadOnlyCollection<string> LATEST_EXTRA_AES = new([
        "0xF959B39D10C93808116F4D0C5583E1D11CBCCD428E737A48B75D40EC87FBF9D8",
        "0xFCFC4D709BC395492703482C50DC423744B5931272587ACCD78B0E57D7215BDD",
        "0x9F3F11DA58B6DD43266CE124F60E955C4A6BE7D5E4B23B69E63EFB0718DA952B",
        "0xD7BA72F24C18357A2384399D98ACF9DB40DD03A55ED4128A396D3D7697930FB5"
    ]);
    
    public const string DISCORD_URL = "https://discord.gg/X7dzY6TUzw";
    public const string TWITTER_URL = "https://twitter.com/RivalsPorting";
    public const string GITHUB_URL = "https://github.com/bmarquez1997/RivalsPorting";
    public const string KOFI_URL = "https://ko-fi.com/halfuwu";
}