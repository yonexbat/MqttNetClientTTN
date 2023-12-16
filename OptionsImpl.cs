using Microsoft.Extensions.Options;

namespace TtnClient;

public class OptionsImpl<T>(T value) : IOptions<T>
    where T : class
{
    public T Value { get; init; } = value;
}