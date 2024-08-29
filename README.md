# Service Bus Tracing Demo

This is a demo to show how to enable tracing in the Azure Service Bus SDK.

This demo uses the Azure Service Bus SDK and OpenTelemetry SDK to export traces to Grafana Cloud when appropriate environeent variables are set.

Clone this repository and run the following commands to start the demo:

```bash
dotnet run
```

This will cause the demo to send and receive messages from a topic and subscription in Azure Service Bus.

To only send messages, run the following command:

```bash
dotnet run -- send
```

To only receive messages, run the following command:

```bash
dotnet run -- receive
```

## Environment Variables

The following environment variables are required to run the demo:

- `DEMO_CONNECTION` - The connection string to the Azure Service Bus namespace.
- `DEMO_TOPIC` - The name of the topic to send/receive from. Defaults to `demotopic`.
- `DEMO_SUBSCRIPTION` - The name of the subscription to receive from. Defaulfs to `demosub`.

Set the following variables to export traces via OTLP. [See the Grafana docs on where to find these values](https://grafana.com/docs/grafana-cloud/monitor-applications/application-observability/otlp-gateway/).
- `OTEL_EXPORTER_OTLP_PROTOCOL` - The protocol to use for the OTLP exporter. Use `http/protobuf` when sending via OTLP to Grafana Cloud.
- `OTEL_EXPORTER_OTLP_ENDPOINT` - The endpoint to send OpenTelemetry traces to. Defaults to `http://localhost:4317`.
- `OTEL_EXPORTER_OTLP_HEADERS` - The headers to send to the OTLP endpoint.
- `OTEL_RESOURCE_ATTRIBUTES` - The resource attributes to send with traces. This should be used to set the service name.

Example of setting service name:

```bash
OTEL_RESOURCE_ATTRIBUTES="service.namespace=local,service.name=send-demo"
```
