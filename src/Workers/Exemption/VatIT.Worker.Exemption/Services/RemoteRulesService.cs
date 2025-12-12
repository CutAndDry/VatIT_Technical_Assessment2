using System.Net.Http.Json;
using System.Text.Json;

namespace VatIT.Worker.Exemption.Services;

public class RemoteRulesService
{
    private readonly IHttpClientFactory _http;
    private readonly ILogger<RemoteRulesService> _logger;
    private readonly Timer _timer;

    public JsonElement? Latest { get; private set; }

    public RemoteRulesService(IHttpClientFactory http, ILogger<RemoteRulesService> logger)
    {
        _http = http;
        _logger = logger;
        _timer = new Timer(async _ => await RefreshAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(15));
    }

    public async Task RefreshAsync()
    {
        try
        {
            var client = _http.CreateClient("orchestrator");
            var res = await client.GetAsync("/admin/rules/exemption");
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogDebug("No remote rules for exemption: {Status}", res.StatusCode);
                return;
            }
            var json = await res.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            Latest = doc.RootElement.Clone();
            _logger.LogDebug("Fetched remote exemption rules");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to refresh exemption rules");
        }
    }
}
