using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using VatIT.Orchestrator.Api.Services;

namespace VatIT.Orchestrator.Api.Controllers
{
    [ApiController]
    [Route("admin/rules")]
    public class RulesController : ControllerBase
    {
        private readonly IRulesRepository _repo;

        public RulesController(IRulesRepository repo)
        {
            _repo = repo;
        }

        [HttpGet("{worker}")]
        public async Task<IActionResult> Get(string worker)
        {
            var rules = await _repo.GetRulesAsync(worker);
            if (rules == null) return NotFound();
            return Ok(rules);
        }

        [HttpPut("{worker}")]
        public async Task<IActionResult> Put(string worker, [FromBody] JsonElement rules, [FromQuery] string note = null)
        {
            await _repo.SaveRulesAsync(worker, rules, note);
            return Ok(new { ok = true });
        }

        [HttpGet("{worker}/versions")]
        public async Task<IActionResult> Versions(string worker)
        {
            var versions = await _repo.GetVersionsAsync(worker);
            return Ok(versions);
        }

        [HttpPost("{worker}/validate")]
        public IActionResult Validate(string worker, [FromBody] JsonElement rules)
        {
            // very small validation: must be object with `rules` array
            if (rules.ValueKind != JsonValueKind.Object || !rules.TryGetProperty("rules", out var arr) || arr.ValueKind != JsonValueKind.Array)
            {
                return BadRequest(new { error = "Rules document must be an object with a top-level 'rules' array." });
            }

            return Ok(new { ok = true });
        }

        [HttpPost("{worker}/evaluate")]
        public async Task<IActionResult> Evaluate(string worker, [FromBody] JsonElement payload)
        {
            // payload: { input: {...} } or { input:..., rules: ... }
            JsonElement? rules = null;
            JsonElement input;
            if (payload.TryGetProperty("rules", out var r)) rules = r;
            if (!payload.TryGetProperty("input", out input)) return BadRequest(new { error = "Missing 'input' in body" });

            var res = await _repo.EvaluateAsync(worker, input, rules);
            if (res == null) return NotFound();
            return Ok(res);
        }
    }
}
