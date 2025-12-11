var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Kestrel to listen on port 8001
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(8001);
});

var app = builder.Build();

app.UseRouting();
app.MapControllers();

app.Logger.LogInformation("Validation Worker starting on port 8001...");

app.Run();
