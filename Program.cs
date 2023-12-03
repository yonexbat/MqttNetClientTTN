// See https://aka.ms/new-console-template for more information

using System.Reflection;
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
await client.Connect();

Console.ReadLine();

Console.WriteLine("By by");

static IConfiguration GetConfiguration(string[] args)
{
    return new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", true, true)
        .AddEnvironmentVariables()
        .AddUserSecrets(Assembly.GetExecutingAssembly())
        .AddCommandLine(args)
        .Build();
}