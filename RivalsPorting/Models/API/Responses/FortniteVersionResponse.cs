using System.Collections.Generic;

namespace RivalsPorting.Models.API.Responses;

public class FortniteVersionResponse
{
    public string Version;
    public FortniteVersionKeys Keys;
    public FortniteVersionMappings? Mappings;
}

public class FortniteVersionKeys
{
    public AesKey MainKey;
    public List<AesKey> ExtraKeys = [];
}

public class FortniteVersionMappings
{
    public string Url;
    public string Md5Hash;
}
