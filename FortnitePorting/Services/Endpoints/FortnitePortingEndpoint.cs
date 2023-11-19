﻿using System.Threading.Tasks;
using FortnitePorting.Services.Endpoints.Models;
using RestSharp;

namespace FortnitePorting.Services.Endpoints;

public class FortnitePortingEndpoint : EndpointBase
{
    private const string CHANGELOG_URL = "https://halfheart.dev/fortnite-porting/api/v2/changelog.json";
    private const string FEATURED_URL = "https://halfheart.dev/fortnite-porting/api/v2/featured.json";

    public FortnitePortingEndpoint(RestClient client) : base(client)
    {
    }

    public async Task<ChangelogResponse[]?> GetChangelogsAsync()
    {
        return await ExecuteAsync<ChangelogResponse[]>(CHANGELOG_URL);
    }

    public ChangelogResponse[]? GetChangelogs()
    {
        return GetChangelogsAsync().GetAwaiter().GetResult();
    }

    public async Task<FeaturedResponse[]?> GetFeaturedAsync()
    {
        return await ExecuteAsync<FeaturedResponse[]>(FEATURED_URL);
    }

    public FeaturedResponse[]? GetFeatured()
    {
        return GetFeaturedAsync().GetAwaiter().GetResult();
    }
}