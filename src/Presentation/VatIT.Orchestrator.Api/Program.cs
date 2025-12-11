using Microsoft.AspNetCore.Diagnostics;
using VatIT.Application.Interfaces;
using VatIT.Application.Services;
using VatIT.Infrastructure.Configuration;
using VatIT.Infrastructure.Services;
using Polly;
using Polly.Timeout;
using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Bind Orchestrator to explicit local ports to avoid accidental conflicts on 5000
// Change these if you need different ports on your machine.
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5100); // HTTP
    // Uncomment the line below to enable HTTPS on 5101 (requires dev cert)
    // options.ListenLocalhost(5101, listenOptions => listenOptions.UseHttps());
});

// Configure Swagger generation with XML comments (if present)
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "VatIT Orchestrator API", Version = "v1" });
    var xmlFile = System.IO.Path.ChangeExtension(typeof(Program).Assembly.Location, ".xml");
    if (System.IO.File.Exists(xmlFile))
    {
        c.IncludeXmlComments(xmlFile);
    }
});

// Configure worker endpoints
builder.Services.Configure<WorkerEndpoints>(
    builder.Configuration.GetSection("WorkerEndpoints"));

// Register application services
builder.Services.AddScoped<IOrchestrationService, OrchestrationService>();

// Register HTTP client for worker communication with resilience and tuning
builder.Services.AddHttpClient<IWorkerClient, WorkerClient>()
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        MaxConnectionsPerServer = 100,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    })
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(5);
    })
    .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(5)))
    .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(2, _ => TimeSpan.FromMilliseconds(200)))
    .AddTransientHttpErrorPolicy(p => p.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));

// Response compression to reduce serialization and network overhead in dev
builder.Services.AddResponseCompression();

var app = builder.Build();

// Configure the HTTP request pipeline.
// Global exception handler: returns JSON error details and prevents unhandled exceptions from crashing the host.
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.ContentType = "application/json";

        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var ex = feature?.Error;

        var result = new { error = ex?.Message ?? "An unexpected error occurred." };
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(result);
    });
});

// Always enable Swagger UI for easy local testing. In production you may want
// to gate this behind environment checks or authentication.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "VatIT Orchestrator API v1");
    options.RoutePrefix = "swagger"; // serve at /swagger
});

app.UseAuthorization();

app.MapControllers();

app.Run();
