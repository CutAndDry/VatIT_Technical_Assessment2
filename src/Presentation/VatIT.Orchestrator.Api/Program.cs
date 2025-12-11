using Microsoft.AspNetCore.Diagnostics;
using VatIT.Application.Interfaces;
using VatIT.Application.Services;
using VatIT.Infrastructure.Configuration;
using VatIT.Infrastructure.Services;
using Polly;

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
        // Allow longer timeouts locally for flaky/high-load tests; Polly will enforce a slightly shorter timeout.
        client.Timeout = TimeSpan.FromSeconds(60);
    })
    // Register correlation handler used to propagate X-Correlation-ID to worker calls
    .AddHttpMessageHandler<VatIT.Orchestrator.Api.Handlers.CorrelationHandler>()
    // Bulkhead to limit concurrent calls to downstream workers and avoid cascading failures under heavy load
    // Increased limits for local performance testing; tune these for your environment.
    .AddPolicyHandler(Policy.BulkheadAsync<HttpResponseMessage>(maxParallelization: 200, maxQueuingActions: 800))
    .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(55)))
    .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(3, attempt =>
    {
        // exponential backoff with small jitter
        var backoffMs = Math.Pow(2, attempt) * 200;
        var jitter = Random.Shared.Next(0, 200);
        return TimeSpan.FromMilliseconds(backoffMs + jitter);
    }))
    .AddTransientHttpErrorPolicy(p => p.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));

builder.Services.AddTransient<VatIT.Orchestrator.Api.Handlers.CorrelationHandler>();
// Make HttpContext available for correlation propagation
builder.Services.AddHttpContextAccessor();

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

// Correlation ID middleware: ensure every request has an X-Correlation-ID and include it in log scope
app.Use(async (context, next) =>
{
    const string headerName = "X-Correlation-ID";
    if (!context.Request.Headers.ContainsKey(headerName))
    {
        context.Request.Headers[headerName] = Guid.NewGuid().ToString();
    }

    using (var scope = app.Logger.BeginScope(new Dictionary<string, object>
    {
        ["CorrelationId"] = context.Request.Headers[headerName].ToString()
    }))
    {
        await next();
    }
});

app.MapControllers();

app.Run();
