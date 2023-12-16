// See https://aka.ms/new-console-template for more information

using System.Reflection;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MQTTnet;
using TtnClient;


Console.WriteLine("Starting up MQTT Client for the things network");
IConfiguration config = GetConfiguration(args);


var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services
            .Configure<TtnClientOptions>(config.GetSection("ClientOptions"));

        services
            .AddSingleton<MqttFactory>()
            .AddSingleton<TtnMqttClient>();

        services
            .AddHostedService<TtnMqttClient>(serviceProvider => serviceProvider.GetRequiredService<TtnMqttClient>());

    })
    .Build();

await host.StartAsync();

var deviceId = config.GetValue<string>("deviceId") ?? throw new ArgumentNullException("deviceId");
var client = host.Services.GetRequiredService<TtnMqttClient>();

client.MessageEvent += GotMessage;

Console.WriteLine("Press any key to terminate");

while (true)
{
    var line = Console.ReadLine();
    
    if (line == "exit")
    {
        await client.Stop();
        break;
    }

    if (line is not null)
    {
        var bytesToSend = Encoding.UTF8.GetBytes(line);
        await client.Publish(deviceId, bytesToSend);
    }
}
await client.Stop();
await host.StopAsync();

Console.WriteLine("By by");

#pragma warning disable 1998
static async Task GotMessage(Message message)
#pragma warning restore
{
    string payload = message.InnerPayload switch
    {
        not null => Encoding.UTF8.GetString(message.InnerPayload),
        _ => string.Empty
    };
    
    Console.WriteLine($"Got message, topic: {message.Topic}, payload: {payload}");
}

static IConfiguration GetConfiguration(string[] args)
{
    return new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", true, true)
        .AddEnvironmentVariables()
        .AddUserSecrets(Assembly.GetExecutingAssembly())
        .AddCommandLine(args)
        .Build();
}