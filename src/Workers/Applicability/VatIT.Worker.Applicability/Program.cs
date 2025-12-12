var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
// Register rule engine
builder.Services.AddSingleton<VatIT.Worker.Applicability.Services.IApplicabilityRuleEngine, VatIT.Worker.Applicability.Services.ApplicabilityRuleEngine>();

// Configure Kestrel to listen on port 8002
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(8002);
});

var app = builder.Build();

app.UseRouting();
app.MapControllers();

app.Logger.LogInformation("Applicability Worker starting on port 8002...");

app.Run();
