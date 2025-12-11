var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Kestrel to listen on port 8003
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(8003);
});

var app = builder.Build();

app.UseRouting();
app.MapControllers();

app.Logger.LogInformation("Exemption Worker starting on port 8003...");

app.Run();
