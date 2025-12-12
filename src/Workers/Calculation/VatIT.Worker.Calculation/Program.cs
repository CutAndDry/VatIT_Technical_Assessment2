var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Kestrel to listen on port 8004
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(8004);
});

// register an HttpClient pointing to the orchestrator API
builder.Services.AddHttpClient("orchestrator", client => {
    client.BaseAddress = new Uri("http://localhost:5100");
});

builder.Services.AddSingleton<VatIT.Worker.Calculation.Services.RemoteRulesService>();

var app = builder.Build();

app.UseRouting();
app.MapControllers();

app.Logger.LogInformation("Calculation Worker starting on port 8004...");

app.Run();
