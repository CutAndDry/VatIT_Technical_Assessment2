using System.Text.Json;
using System.Threading.Tasks;

namespace VatIT.Orchestrator.Api.Services
{
    public interface IRulesRepository
    {
        Task<JsonElement?> GetRulesAsync(string worker);
        Task SaveRulesAsync(string worker, JsonElement rules, string note = null);
        Task<JsonElement[]> GetVersionsAsync(string worker);
        Task<JsonElement?> EvaluateAsync(string worker, JsonElement input, JsonElement? rulesOverride = null);
    }
}
