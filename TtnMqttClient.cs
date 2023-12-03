using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;

namespace TtnClient;

public class TtnMqttClient(TtnClientOptions options, MqttFactory factory, ILogger<TtnMqttClient> logger)
{
   public async Task Connect(Func<Message, Task> callback)
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
      
      mqttClient.ApplicationMessageReceivedAsync += e => HandleMessage(e, callback);
      
      var mqttSubscribeOptions = factory.CreateSubscribeOptionsBuilder()
         .WithTopicFilter(
            f =>
            {
               f.WithTopic("#");
            })
         .Build();

      await mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);
      
      logger.LogInformation("Waiting for messages");
      await Task.Delay(1000000);
   }

   private async Task HandleMessage(MqttApplicationMessageReceivedEventArgs e, Func<Message, Task> callback)
   {
      logger.LogInformation("Got message:");
      var payload = e.ApplicationMessage?.PayloadSegment.Array;
      byte[]? innerPayload = null;
      if (payload is not null)
      {
        innerPayload = HandlePayload(payload);
      }
      
      Message message = new Message(e.ApplicationMessage?.Topic ?? string.Empty, innerPayload, payload);
      logger.LogDebug("Calling callback");
      await callback(message);
      logger.LogDebug("Callback called");
   }

   private byte[]? HandlePayload(byte[] payload)
   {
      var text = Encoding.UTF8.GetString(payload);
      var lora = JsonSerializer.Deserialize<LoRaMessage>(text);

      var innerPayload = lora?.UplinkMessage?.DecodedPayload?.Bytes switch
      {
         not null => lora.UplinkMessage?.DecodedPayload?.Bytes
            .Select(Convert.ToByte)
            .ToArray(),
         _ => null,
      };
      
      logger.LogInformation("Got inner payload: {0}", innerPayload is not null);
      return innerPayload;
   }
}