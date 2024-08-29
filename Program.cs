using Azure.Messaging.ServiceBus;
using Grafana.OpenTelemetry;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using System.Globalization;

var connectionString = Environment.GetEnvironmentVariable("DEMO_CONNECTION");
var topicName = Environment.GetEnvironmentVariable("DEMO_TOPIC") ?? "demotopic";
var subscriptionName = Environment.GetEnvironmentVariable("DEMO_SUB") ?? "demosub";

// enable experimental Azure SDK tracing support
AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

var enableSender = args.Length > 0 ? args.Contains("send") : true;
var enableReceiver = args.Length > 0 ? args.Contains("receive") : true;
var serviceName = (enableSender, enableReceiver) switch
{
    (true, false) => "ServiceBusSender",
    (false, true) => "ServiceBusReceiver",
    _ => "ServiceBusDemo"
};

Console.WriteLine($"Sending: {enableSender}, Receiving: {enableReceiver}");

using var tracing = Sdk.CreateTracerProviderBuilder()
    // subscribe to Azure SDK events
    .AddSource("Azure.*")
    .UseGrafana()
    .AddConsoleExporter()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
    .Build();

var client = new ServiceBusClient(connectionString, new ServiceBusClientOptions
{
    TransportType = ServiceBusTransportType.AmqpTcp,
});
Console.WriteLine($"Client created. Transport: {client.TransportType}, Namespace: {client.FullyQualifiedNamespace}");

var sender = client.CreateSender(topicName);
var processor = client.CreateProcessor(topicName, subscriptionName, new ServiceBusProcessorOptions
{
    AutoCompleteMessages = false,
    MaxConcurrentCalls = 1,
});

var cancel = new CancellationTokenSource();

var sending = Task.Run(async () =>
{
    if (!enableSender) return;

    while (true)
    {
        for (int i = 0; i < 10; i++)
        {
            var message = new ServiceBusMessage($"Hello, World! {i} {DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}");
            Console.WriteLine("Sent: " + message.Body.ToString());
            await sender.SendMessageAsync(message, cancellationToken: cancel.Token);
        }
        await Task.Delay(5000, cancel.Token);
    }
}, cancel.Token);

async Task MessageHandler(ProcessMessageEventArgs args)
{
    Console.WriteLine("Received: " + args.Message.Body.ToString());
    await args.CompleteMessageAsync(args.Message);
}

Task ErrorHandler(ProcessErrorEventArgs args)
{
    Console.WriteLine(args.Exception.ToString());
    return Task.CompletedTask;
}

try
{
    processor.ProcessMessageAsync += MessageHandler;
    processor.ProcessErrorAsync += ErrorHandler;
    await processor.StartProcessingAsync();

    Console.ReadKey();
    cancel.Cancel();

    await processor.StopProcessingAsync();
}
finally
{
    await processor.DisposeAsync();
    await sender.DisposeAsync();
    await client.DisposeAsync();
}
