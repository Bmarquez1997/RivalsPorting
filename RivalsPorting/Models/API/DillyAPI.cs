using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using CUE4Parse.Utils;
using RivalsPorting.Models.API.Base;
using RivalsPorting.Models.API.Requests;
using RivalsPorting.Models.API.Responses;
using RivalsPorting.Models.Map;
using Mapster;
using RestSharp;

namespace RivalsPorting.Models.API;

public class DillyAPI(RestClient client) : APIBase(client)
{
    protected override string BaseURL => "https://export-service-new.dillyapis.com/v1";

    public async Task<ManifestRequest[]> Manifests() => await ExecuteAsync<ManifestRequest[]>("manifests") ?? [];

}