using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;

namespace TtnClient;

public class TtnMqttClient(IOptions<TtnClientOptions> options, MqttFactory factory, ILogger<TtnMqttClient> logger) : BackgroundService
{
    private readonly Queue<MessageContainer> _messagesToSend = new();
    private readonly SemaphoreSlim _semaphore = new(1);

    private readonly SemaphoreSlim _wakeUpSemaphore = new(0, 1);

    private IManagedMqttClient? _client;

    public event Func<Message, Task>? MessageEvent;

    private volatile bool _exitPending = false;
    

    public async Task Publish(string device, byte[] payload)
    {
        await _semaphore.WaitAsync();
        try
        {
            var message = new MessageContainer(device, payload);
            _messagesToSend.Enqueue(message);
        }
        finally
        {
            _semaphore.Release();
        }

        _wakeUpSemaphore.Release();
    }

    public async Task Stop()
    {
        await _semaphore.WaitAsync();
        _exitPending = true;
        _wakeUpSemaphore.Release();
        _semaphore.Release();
    }

    private async Task Connect()
    {
        var ttnOptions = options.Value ?? throw new ArgumentException("Options value must not be null");

        _client = factory.CreateManagedMqttClient();

        var url = $"{ttnOptions.Region ?? throw new ArgumentException("Region")}.cloud.thethings.network";

        var mqttClientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(url)
            .WithCredentials(ttnOptions.UserId ?? throw new ArgumentException("UserId"),
                ttnOptions.AccessKey ?? throw new ArgumentException("AccessKey"))
            .Build();

        var managedMqttClientOptions = new ManagedMqttClientOptionsBuilder()
            .WithClientOptions(mqttClientOptions)
            .Build();

        await _client.StartAsync(managedMqttClientOptions);

        _client.ApplicationMessageReceivedAsync += HandleMessage;

        await _client.SubscribeAsync("#");

        logger.LogInformation("Waiting for messages");
        while (!_exitPending)
        {
            await _wakeUpSemaphore.WaitAsync();
            await SendMessages();
        }

        _client.Dispose();
    }

    private async Task SendMessages()
    {
        await _semaphore.WaitAsync();
        logger.LogDebug("Sending enqueued messages.");
        try
        {
            while (_messagesToSend.Count > 0)
            {
                var messageToSend = _messagesToSend.Dequeue();
                await PublishInternal(messageToSend.Device, messageToSend.Payload);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task HandleMessage(MqttApplicationMessageReceivedEventArgs e)
    {
        logger.LogInformation("Got mqtt event.");
        var payload = e.ApplicationMessage?.PayloadSegment.Array;
        byte[]? innerPayload = null;
        if (payload is not null)
        {
            innerPayload = HandlePayload(payload);
        }

        var message = new Message(e.ApplicationMessage?.Topic ?? string.Empty, innerPayload, payload);
        await FireEvent(message);
    }

    private async Task FireEvent(Message message)
    {
        logger.LogDebug("Calling subscribers");
        if (MessageEvent is not null)
        {
            try
            {
                await MessageEvent.Invoke(message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }
        }
        else
        {
            logger.LogDebug("No subscribers registered");
        }

        logger.LogDebug("Subscribers called");
    }

    private async Task PublishInternal(string device, byte[] payload)
    {
        if (_client is null)
        {
            throw new InvalidOperationException("Call start-server first");
        }

        var payloadAsBase64 = Convert.ToBase64String(payload);
        var message = new DownLinkMessage
        {
            DownLinks = new List<DownLink>
            {
                new()
                {
                    FrmPayload = payloadAsBase64,
                    Priority = "NORMAL",
                    FPort = 15,
                    CorrelationIds = new List<string>
                        { Guid.NewGuid().ToString() },
                },
            },
        };
        var messageAsString = JsonSerializer.Serialize(message);
        var topic = $"v3/{options.Value.UserId}/devices/{device}/down/push";
        logger.LogDebug("Topic: {topic}", topic);


        var applicationMessage = new MqttApplicationMessageBuilder()
            .WithTopic($"v3/{options.Value.UserId}/devices/{device}/down/push")
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogDebug("ExecuteAsync");
        try
        {
            await Connect();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            throw;
        }
        logger.LogDebug("Done");
    }
}