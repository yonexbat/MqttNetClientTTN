// See https://aka.ms/new-console-template for more information

using System.Reflection;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MQTTnet;
using TtnClient;


Console.WriteLine("Starting up MQTT Client for the things network");
IConfiguration config = GetConfiguration(args);

var userId = config.GetValue<string>("userId") ?? throw new ArgumentNullException("userId");
var accessKey = config.GetValue<string>("accessKey") ?? throw new ArgumentNullException("accessKey");
var region = config.GetValue<string>("region") ?? throw new ArgumentNullException("region");
var deviceId = config.GetValue<string>("deviceId") ?? throw new ArgumentNullException("deviceId");

var options = new TtnClientOptions()
{
    Region = region,
    AccessKey = accessKey,
    UserId = userId,
};

using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
ILogger<TtnMqttClient> logger = factory.CreateLogger<TtnMqttClient>();

TtnMqttClient client = new TtnMqttClient(options, new MqttFactory(), logger);

client.MessageEvent += GotMessage;

Console.WriteLine("Press any key to terminate");
var res = client.StartClient();

while (true)
{
    var line = Console.ReadLine();
    
    if (line == "exit")
    {
        break;
    }

    if (line is not null)
    {
        var bytesToSend = Encoding.UTF8.GetBytes(line);
        await client.Publish(deviceId, bytesToSend);
    }
}
await client.Stop();
await res;

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