using System.Text.Json.Serialization;

namespace TtnClient;

public record DownLinkMessage
{
    [JsonPropertyName("downlinks")] public required ICollection<DownLink> DownLinks { get; init; }
}

public record DownLink
{
    [JsonPropertyName("f_port")] public ushort FPort { get; set; }
    
    [JsonPropertyName("frm_payload")]  public required string FrmPayload { get; init; } 
    
    [JsonPropertyName("priority")] public required string Priority { get; init; }
    
    [JsonPropertyName("correlation_ids")] public required ICollection<string> CorrelationIds { get; init; }
}