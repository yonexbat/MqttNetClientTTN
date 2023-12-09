using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;

namespace TtnClient;

public class TtnMqttClient(TtnClientOptions options, MqttFactory factory, ILogger<TtnMqttClient> logger)
{
   
   public event Func<Message, Task>? MessageEvent;

   public Task StartClient()
   {
      return Task.Run(async () =>
      {
         await Connect();
      });
   }
   
   private async Task Connect()
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
      
      mqttClient.ApplicationMessageReceivedAsync += e => HandleMessage(e);
      
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

   private async Task HandleMessage(MqttApplicationMessageReceivedEventArgs e)
   {
      logger.LogInformation("Got message:");
      var payload = e.ApplicationMessage?.PayloadSegment.Array;
      byte[]? innerPayload = null;
      if (payload is not null)
      {
        innerPayload = HandlePayload(payload);
      }
      
      Message message = new Message(e.ApplicationMessage?.Topic ?? string.Empty, innerPayload, payload);
      
      await FireEvent(message);
   }

   private async Task FireEvent(Message message)
   {
      logger.LogDebug("Calling subscribers");
      if (this.MessageEvent is not null)
      {
         await this.MessageEvent.Invoke(message);
      }
      else
      {
         logger.LogDebug("No subscribers registered");
      }
      logger.LogDebug("Subscribers called");
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