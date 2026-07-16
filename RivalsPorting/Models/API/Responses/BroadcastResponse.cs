using System;
using Newtonsoft.Json;

namespace RivalsPorting.Models.API.Responses;

public class BroadcastResponse
{
    [JsonProperty("entries")] public BroadcastResponseEntry[] Entries { get; set; }
}

public class BroadcastResponseEntry
{
    [JsonProperty("title")] public string Title { get; set; }
    [JsonProperty("description")] public string Description { get; set; }
    [JsonProperty("timestamp")] public DateTime Timestamp { get; set; }
    [JsonProperty("minVersion")] public RPVersion? MinVersion { get; set; }
    [JsonProperty("maxVersion")] public RPVersion? MaxVersion { get; set; }
    [JsonProperty("isEnabled")] public bool IsEnabled { get; set; }
}