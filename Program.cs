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

// force AMQP transport
// ensures HttpClient instrumentation is not used
var options = new ServiceBusClientOptions
{
    TransportType = ServiceBusTransportType.AmqpTcp,
};
await using var client = new ServiceBusClient(connectionString, options);
Console.WriteLine($"Client created. Transport: {client.TransportType}, Namespace: {client.FullyQualifiedNamespace}");

var sender = client.CreateSender(topicName);
var receiver = client.CreateReceiver(topicName, subscriptionName);

var cancel = new CancellationTokenSource();
var tasks = new List<Task>();

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
tasks.Add(sending);

var receiving = Task.Run(async () =>
{
    if (!enableReceiver) return;

    while (true)
    {
        var messages = await receiver.ReceiveMessagesAsync(10, maxWaitTime: TimeSpan.FromSeconds(5), cancellationToken: cancel.Token);

        foreach (var message in messages)
        {
            Console.WriteLine("Received: " + message.Body.ToString());
            await receiver.CompleteMessageAsync(message, cancel.Token);
        }

        await Task.Delay(1000, cancel.Token);
    }
}, cancel.Token);
tasks.Add(receiving);

Console.CancelKeyPress += (s, e) =>
{
    cancel.Cancel();
    e.Cancel = true;
};

try
{
    await Task.WhenAll(tasks.ToArray());
}
finally
{
    await receiver.DisposeAsync();
    await sender.DisposeAsync();
    await client.DisposeAsync();
}
