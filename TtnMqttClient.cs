using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;

namespace TtnClient;

public class TtnMqttClient(TtnClientOptions options, MqttFactory factory, ILogger<TtnMqttClient> logger)
{
   
   public event Func<Message, Task>? MessageEvent;

   private IManagedMqttClient? _client;
   
   private SemaphoreSlim _semaphore = new SemaphoreSlim(1);
   private IList<MessageContainer> _messagesToSend = new List<MessageContainer>();
   

   public Task StartClient()
   {
      return Task.Run(async () =>
      {
         await Connect();
      });
   }
   
   private async Task Connect()
   {
      _client =  factory.CreateManagedMqttClient();
      string url = $"{options.Region}.cloud.thethings.network";
      
      var mqttClientOptions = new MqttClientOptionsBuilder()
         .WithTcpServer(url)
         .WithCredentials(options.UserId, options.AccessKey)
         .Build();
      
      var managedMqttClientOptions = new ManagedMqttClientOptionsBuilder()
         .WithClientOptions(mqttClientOptions)
         .Build();
      
      await _client.StartAsync(managedMqttClientOptions);

      /*
      if (response.ResultCode != MqttClientConnectResultCode.Success)
      {
         var responseAsString = JsonSerializer.Serialize(response);
         logger.LogError("Could not connect. Response: {response}", responseAsString);
         throw new Exception(responseAsString);
      }*/
      
      _client.ApplicationMessageReceivedAsync += HandleMessage;

      await _client.SubscribeAsync("#");
      
      logger.LogInformation("Waiting for messages");
      while (true)
      {
         await Task.Delay(1000);
         await SendMessages();
      }
   }

   private async Task SendMessages()
   {
      await _semaphore.WaitAsync();
      foreach (var message in _messagesToSend)
      {
         await PublishInternal(message.Device, message.Payload);
      }
      _messagesToSend.Clear();
      _semaphore.Release();
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
   
   public async Task Publish(string device, byte[] payload)
   {
      await _semaphore.WaitAsync();
      this._messagesToSend.Add(new MessageContainer(device, payload));
      _semaphore.Release();
   }   
   
   private async Task PublishInternal(string device, byte[] payload)
   {
      if (_client is null)
      {
         throw new InvalidOperationException("Call start-server first");
      }
      
      string payloadAsBase64 = Convert.ToBase64String(payload);
      DownLinkMessage message = new DownLinkMessage
      {
         DownLinks = new List<DownLink>()
         {
            new DownLink
            {
               FrmPayload = payloadAsBase64,
               Priority = "NORMAL",
               FPort = 15,
               CorrelationIds = new List<string>(){ Guid.NewGuid().ToString()},
            },
         },
      };
      string messageAsString = JsonSerializer.Serialize(message);
      string topic = $"v3/{options.UserId}/devices/{device}/down/push";
      logger.LogDebug("Topic: {topic}", topic);
      
      
      var applicationMessage = new MqttApplicationMessageBuilder()
         .WithTopic($"v3/{options.UserId}/devices/{device}/down/push")
         .WithPayload(messageAsString)
         .Build();


      await _client.EnqueueAsync(applicationMessage);
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