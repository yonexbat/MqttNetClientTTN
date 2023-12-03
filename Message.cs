namespace TtnClient;

public record Message(string Topic, byte[]? InnerPayload, byte[]? RawMessage);