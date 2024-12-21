using System;
using System.Threading.Tasks;
using FortnitePorting.Shared;
using FortnitePorting.Shared.Models.API;
using RestSharp;

namespace FortnitePorting.Models.API;

public class FortnitePortingServerAPI(RestClient client) : APIBase(client)
{
    public async Task SendAsync(string data, EExportServerType serverType)
    {
        if (serverType == EExportServerType.None) return;
        
        var port = (int) serverType;
        var serverUrl = $"http://127.0.0.1:{port}/rivals-porting/data";
        await ExecuteAsync(serverUrl, method: Method.Post, verbose: false, parameters: new BodyParameter(data, ContentType.Json));
    }
    
    public async Task<bool> PingAsync(EExportServerType serverType)
    {
        if (serverType == EExportServerType.None) return false;
        
        var port = (int) serverType;
        var serverUrl = $"http://127.0.0.1:{port}/rivals-porting/ping";
        var response = await ExecuteAsync(serverUrl, method: Method.Get, verbose: false);
        return response.IsSuccessful;
    }
}

public enum EExportServerType
{
    None = -1,
    
    Blender = 20025,
    Unreal = 20001,
    Unity = 20002
}

public static class EExportServerTypeExtensions
{
    public static EExportServerType ToServerType(this EExportLocation exportLocation) => exportLocation switch
    {
        EExportLocation.Blender => EExportServerType.Blender,
        EExportLocation.Unreal => EExportServerType.Unreal,
        EExportLocation.Unity => EExportServerType.Unity,
        _ => EExportServerType.None
    };
}