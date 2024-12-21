using System.Collections.Generic;
using System.IO;
using System.Linq;
using CUE4Parse.FileProvider.Vfs;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;
using CUE4Parse.Utils;

namespace FortnitePorting.Models.CUE4Parse;

public class HybridFileProvider : AbstractVfsFileProvider
{
    public bool LoadExtraDirectories;
    private readonly bool IsOptionalLoader;
    private readonly DirectoryInfo WorkingDirectory;
    private readonly IEnumerable<DirectoryInfo> ExtraDirectories;
    private const bool CaseInsensitive = true;
    private const SearchOption SearchOption = System.IO.SearchOption.AllDirectories;
    
    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        RecurseSubdirectories = SearchOption == SearchOption.AllDirectories,
        IgnoreInaccessible = true,
    };

    public HybridFileProvider(VersionContainer? version = null, bool isOptionalLoader = false)  : base(CaseInsensitive, version)
    {
        IsOptionalLoader = isOptionalLoader;
        SkipReferencedTextures = true;
    }

    public HybridFileProvider(string directory, List<DirectoryInfo>? extraDirectories = null, VersionContainer? version = null, bool isOptionalLoader = false) : this(version)
    {
        WorkingDirectory = new DirectoryInfo(directory);
        ExtraDirectories = extraDirectories?.Where(dir => dir.Exists) ?? [];
        IsOptionalLoader = isOptionalLoader;
        SkipReferencedTextures = true;
    }

    public override void Initialize()
    {
        if (!WorkingDirectory.Exists) throw new DirectoryNotFoundException($"Provided installation folder does not exist: {WorkingDirectory.FullName}");
        
        RegisterFiles(WorkingDirectory);
        
        if (LoadExtraDirectories)
        {
            foreach (var extraDirectory in ExtraDirectories)
            {
                RegisterFiles(extraDirectory);
            }
        }
    }

    public void RegisterFiles(DirectoryInfo directory)
    {
        foreach (var file in directory.EnumerateFiles("*.*", EnumerationOptions))
        {
            var extension = file.Extension.SubstringAfter('.').ToLower();
            if (extension is not ("pak" or "utoc")) continue;
            RegisterVfs(file.FullName, [ file.OpenRead() ], it => new FStreamArchive(it, File.OpenRead(it), Versions));
        }
    }
}