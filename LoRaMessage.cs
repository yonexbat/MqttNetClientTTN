using System.Text.Json.Serialization;

namespace TtnClient;

public record LoRaMessage
{
    [JsonPropertyName("uplink_message")] public UplinkMessage? UplinkMessage { get; set; }

    [JsonPropertyName("received_at")] public DateTimeOffset? ReceivedAt { get; set; }

    [JsonPropertyName("end_device_ids")] public EndDeviceIds? EndDeviceIds { get; set; }
}

public record UplinkMessage
{
    [JsonPropertyName("decoded_payload")] public DecodedPayload? DecodedPayload { get; set; }
}

public record DecodedPayload
{
    [JsonPropertyName("bytes")] public ushort[]? Bytes { get; set; }
}

public record EndDeviceIds
{
    [JsonPropertyName("device_id")] public string? DeviceId { get; set; }
}