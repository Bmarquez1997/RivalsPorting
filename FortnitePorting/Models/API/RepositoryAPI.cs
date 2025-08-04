using System.Threading.Tasks;
using FortnitePorting.Models.API.Responses;
using FortnitePorting.Shared.Models.API;
using RestSharp;

namespace FortnitePorting.Models.API;

public class RepositoryAPI(RestClient client) : APIBase(client)
{
    public const string REPOSITORY_URL = "https://raw.githubusercontent.com/Bmarquez1997/RivalsPorting/refs/heads/RivalsPorting/Repository.json";
    public const string MAPPINGS_URL = "https://raw.githubusercontent.com/Bmarquez1997/RivalsPorting/refs/heads/RivalsPorting/Mappings.json";
    
    public async Task<RepositoryResponse?> GetRepositoryAsync(string url = REPOSITORY_URL)
    {
        return await ExecuteAsync<RepositoryResponse>(url);
    }
    
    public async Task<MappingsResponse[]?> GetMappingsAsync()
    {
        return await ExecuteAsync<MappingsResponse[]>(MAPPINGS_URL);
    }
}