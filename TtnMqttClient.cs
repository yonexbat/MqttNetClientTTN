using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;

namespace TtnClient;

public class TtnMqttClient(TtnClientOptions options, MqttFactory factory, ILogger<TtnMqttClient> logger)
{
   public async Task Connect()
   {
      using var mqttClient =  factory.CreateMqttClient();
      string url = $"{options.Region}.cloud.thethings.network";
      
      var mqttClientOptions = new MqttClientOptionsBuilder()
         .WithTcpServer(url)
         .WithCredentials(options.UserId, options.AccessKey)
         .Build();
      
      var response = await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

      if (response.ResultCode != MqttClientConnectResultCode.Success)
      {
         var responseAsString = JsonSerializer.Serialize(response);
         logger.LogError("Could not connect. Response: {response}", responseAsString);
         throw new Exception(responseAsString);
      }
      
      mqttClient.ApplicationMessageReceivedAsync += e =>
      {
         logger.LogInformation("Got message:");
         return Task.CompletedTask;
      };
      
      var mqttSubscribeOptions = factory.CreateSubscribeOptionsBuilder()
         .WithTopicFilter(
            f =>
            {
               f.WithTopic("#");
            })
         .Build();

      await mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);

      await Task.Delay(100000);
   }
    
}