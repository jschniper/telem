using System.Diagnostics;
using System.Diagnostics.Metrics;

using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Logs;
using OpenTelemetry.Exporter;

// Define some important constants to initialize tracing with
var serviceName = "MyCompany.MyProduct.MyService";
var serviceVersion = "1.0.0";

var builder = WebApplication.CreateBuilder(args);

var appResourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(serviceName: serviceName, serviceVersion: serviceVersion);

// Add services to the container.
builder.Services.AddRazorPages();

// Configure important OpenTelemetry settings, the console exporter, and instrumentation library
builder.Services.AddOpenTelemetryTracing(tracerProviderBuilder =>
{
    tracerProviderBuilder
        .AddConsoleExporter()
        .AddOtlpExporter(opt =>
        {
            opt.Endpoint = new Uri("otel:4318");
            opt.Protocol = OtlpExportProtocol.HttpProtobuf;
        })
        .AddSource(serviceName)
        .SetResourceBuilder(
            ResourceBuilder.CreateDefault()
                .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
        .AddHttpClientInstrumentation()
        .AddAspNetCoreInstrumentation()
        .AddSqlClientInstrumentation();
});

var meter = new Meter(serviceName);
var counter = meter.CreateCounter<long>("app.request-counter");
builder.Services.AddOpenTelemetryMetrics(metricProviderBuilder =>
{
    metricProviderBuilder
        .AddConsoleExporter()
        .AddOtlpExporter(opt =>
        {
            opt.Endpoint = new Uri("otel:4318");
            opt.Protocol = OtlpExportProtocol.HttpProtobuf;
        })
        .AddMeter(meter.Name)
        .SetResourceBuilder(appResourceBuilder)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

var MyActivitySource = new ActivitySource(serviceName);

var httpClient = new HttpClient();

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        options
        .AddConsoleExporter()
        .AddOtlpExporter(opt =>
        {
            opt.Endpoint = new Uri("otel:4318");
            opt.Protocol = OtlpExportProtocol.HttpProtobuf;
        });
    });
});

app.MapGet("/hello", async () =>
{
    // Track work inside of the request
    using var activity = MyActivitySource.StartActivity("SayHello");
    activity?.SetTag("foo", 1);
    activity?.SetTag("bar", "Hello, World!");
    activity?.SetTag("baz", new int[] { 1, 2, 3 });

    counter.Add(1);

    var logger = loggerFactory.CreateLogger<Program>();

    logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);

    var html = await httpClient.GetStringAsync("https://example.com/");
    if (string.IsNullOrWhiteSpace(html))
    {
        return "Hello, World!";
    }
    else
    {
        return "Hello, World!";
    }
});

app.Run();
