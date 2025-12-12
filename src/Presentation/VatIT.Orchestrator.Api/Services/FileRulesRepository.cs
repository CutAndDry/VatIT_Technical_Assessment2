using System.Text.Json;
using System.Text.Json.Nodes;

namespace VatIT.Orchestrator.Api.Services
{
    // Simple file-backed rules repository for local prototyping.
    // Stores latest rules as {worker}.json and up to 50 version entries in {worker}.versions.json
    public class FileRulesRepository : IRulesRepository
    {
        private readonly string _basePath;

        public FileRulesRepository(IWebHostEnvironment env)
        {
            _basePath = Path.Combine(env.ContentRootPath, "rules");
            // Ensure the content-root rules folder exists for saves/versions
            Directory.CreateDirectory(_basePath);
        }

        // Primary rules path (used for writes)
        private string RulesPath(string worker) => Path.Combine(_basePath, worker + ".json");
        private string VersionsPath(string worker) => Path.Combine(_basePath, worker + ".versions.json");

        // Sometimes the host's ContentRootPath differs from the running binary folder (e.g. when launched via debugger).
        // Try both locations when reading so the dev UI can find seeded rules regardless of host context.
        private IEnumerable<string> CandidateRulesPaths(string worker)
        {
            yield return Path.Combine(_basePath, worker + ".json");
            // fallback: check the base directory where the assembly is running
            var alt = Path.Combine(AppContext.BaseDirectory ?? string.Empty, "rules", worker + ".json");
            if (!string.Equals(alt, Path.Combine(_basePath, worker + ".json"), StringComparison.OrdinalIgnoreCase)) yield return alt;
        }

        private IEnumerable<string> CandidateVersionsPaths(string worker)
        {
            yield return Path.Combine(_basePath, worker + ".versions.json");
            var alt = Path.Combine(AppContext.BaseDirectory ?? string.Empty, "rules", worker + ".versions.json");
            if (!string.Equals(alt, Path.Combine(_basePath, worker + ".versions.json"), StringComparison.OrdinalIgnoreCase)) yield return alt;
        }

        public async Task<JsonElement?> GetRulesAsync(string worker)
        {
            foreach (var path in CandidateRulesPaths(worker))
            {
                if (!File.Exists(path)) continue;
                try
                {
                    using var s = File.OpenRead(path);
                    var doc = await JsonDocument.ParseAsync(s);
                    return doc.RootElement.Clone();
                }
                catch
                {
                    // ignore malformed file and continue to next candidate
                }
            }
            return null;
        }

        public async Task SaveRulesAsync(string worker, JsonElement rules, string note = null)
        {
            var path = RulesPath(worker);
            await using (var fs = File.Create(path))
            {
                await JsonSerializer.SerializeAsync(fs, rules);
            }

            var versions = new List<JsonElement>();
            var vpath = VersionsPath(worker);
            if (File.Exists(vpath))
            {
                try
                {
                    using var s = File.OpenRead(vpath);
                    var doc = await JsonDocument.ParseAsync(s);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in doc.RootElement.EnumerateArray()) versions.Add(el.Clone());
                    }
                }
                catch { /* ignore */ }
            }

            // prepend new version
            var meta = new JsonObject
            {
                ["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ["note"] = note ?? string.Empty,
                ["snapshot"] = JsonNode.Parse(rules.GetRawText())
            };
            versions.Insert(0, JsonDocument.Parse(meta.ToJsonString()).RootElement.Clone());
            // keep last 50
            var toWrite = versions.Take(50).ToArray();
            await using (var fs = File.Create(vpath))
            {
                await JsonSerializer.SerializeAsync(fs, toWrite);
            }
        }

        public async Task<JsonElement[]> GetVersionsAsync(string worker)
        {
            foreach (var vpath in CandidateVersionsPaths(worker))
            {
                if (!File.Exists(vpath)) continue;
                try
                {
                    using var s = File.OpenRead(vpath);
                    var doc = await JsonDocument.ParseAsync(s);
                    if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;
                    return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToArray();
                }
                catch { /* ignore and continue */ }
            }
            return Array.Empty<JsonElement>();
        }

        // NOTE (temporary): Very small evaluator used by the Orchestrator admin UI 'Run Sample'.
        // This is a lightweight preview/mocking implementation intended only for the Rules
        // Admin UX and quick testing. It is NOT the canonical business-rule engine used
        // by the worker services. Production worker engines live inside each worker
        // project (e.g., Applicability/Exemption engines) and should remain the
        // source of truth for runtime behavior.
        //
        // Keep this helper for the UI preview, but do NOT rely on it for production
        // correctness. If you consolidate engines later, consider extracting shared
        // logic into a library and removing this mock evaluator.
        public async Task<JsonElement?> EvaluateAsync(string worker, JsonElement input, JsonElement? rulesOverride = null)
        {
            JsonElement? rules = rulesOverride ?? await GetRulesAsync(worker);
            if (rules == null) return null;

            if (rules.Value.ValueKind == JsonValueKind.Object)
            {
                // 1) rule-array driven engine (existing behaviour)
                if (rules.Value.TryGetProperty("rules", out var rarr) && rarr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var r in rarr.EnumerateArray())
                    {
                        try
                        {
                            if (!r.TryGetProperty("when", out var when)) continue;
                            var field = when.GetProperty("field").GetString();
                            var op = when.TryGetProperty("op", out var opEl) ? opEl.GetString() : "==";
                            var val = when.GetProperty("value");
                            var candidate = input.TryGetProperty(field, out var cand) ? cand : default;
                            bool match = false;
                            switch (op)
                            {
                                case "==": match = candidate.ToString() == val.ToString(); break;
                                case ">": if (candidate.TryGetDouble(out double cd1) && val.TryGetDouble(out double vd1)) match = cd1 > vd1; break;
                                case ">=": if (candidate.TryGetDouble(out double cd2) && val.TryGetDouble(out double vd2)) match = cd2 >= vd2; break;
                                case "<": if (candidate.TryGetDouble(out double cd3) && val.TryGetDouble(out double vd3)) match = cd3 < vd3; break;
                                case "<=": if (candidate.TryGetDouble(out double cd4) && val.TryGetDouble(out double vd4)) match = cd4 <= vd4; break;
                                default: break;
                            }
                            if (match)
                            {
                                if (r.TryGetProperty("then", out var then)) return then.Clone();
                            }
                        }
                        catch { /* ignore malformed rules */ }
                    }
                }

                // 2) exemption-style document: check exemptCustomers or categoryExemptions
                if (rules.Value.TryGetProperty("exemptCustomers", out var exArr) && exArr.ValueKind == JsonValueKind.Array)
                {
                    if (input.TryGetProperty("customerId", out var cid) && cid.ValueKind == JsonValueKind.String)
                    {
                        foreach (var ex in exArr.EnumerateArray())
                        {
                            if (ex.GetString() == cid.GetString())
                            {
                                var matchedDoc = JsonDocument.Parse("{\"exempt\":true,\"reason\":\"customer\"}");
                                return matchedDoc.RootElement.Clone();
                            }
                        }
                    }

                    // category exemptions
                    if (rules.Value.TryGetProperty("categoryExemptions", out var catEx) && catEx.ValueKind == JsonValueKind.Object)
                    {
                        if (input.TryGetProperty("category", out var cat) && cat.ValueKind == JsonValueKind.String)
                        {
                            var c = cat.GetString();
                            if (!string.IsNullOrEmpty(c) && catEx.TryGetProperty(c, out var reasons))
                            {
                                var matchedDoc = JsonDocument.Parse($"{{\"exempt\":true,\"reason\":\"category:{c}\"}}");
                                return matchedDoc.RootElement.Clone();
                            }
                        }
                    }
                }

                // 3) applicability-style document: thresholds + merchantVolumes
                if (rules.Value.TryGetProperty("thresholds", out var thresholds) && thresholds.ValueKind == JsonValueKind.Object)
                {
                    if (input.TryGetProperty("merchantId", out var mid) && mid.ValueKind == JsonValueKind.String)
                    {
                        var mstr = mid.GetString();
                        if (!string.IsNullOrEmpty(mstr) && rules.Value.TryGetProperty("merchantVolumes", out var mvols) && mvols.ValueKind == JsonValueKind.Object)
                        {
                            if (mvols.TryGetProperty(mstr, out var mv) && mv.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var prop in thresholds.EnumerateObject())
                                {
                                    var state = prop.Name;
                                    if (mv.TryGetProperty(state, out var vol) && vol.ValueKind == JsonValueKind.Number && prop.Value.TryGetDouble(out var thr))
                                    {
                                        if (vol.GetDouble() >= thr)
                                        {
                                            var matchedDoc = JsonDocument.Parse($"{{\"applicable\":true,\"state\":\"{state}\"}}");
                                            return matchedDoc.RootElement.Clone();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // 4) calculation defaults: if a defaults.rate present, return it
                if (rules.Value.TryGetProperty("defaults", out var defs) && defs.ValueKind == JsonValueKind.Object)
                {
                    if (defs.TryGetProperty("rate", out var rate) && rate.ValueKind == JsonValueKind.Number)
                    {
                        // fallback: return a simple matched object with rate
                        var json = JsonSerializer.Serialize(new { matched = new { rate = rate.GetDouble() } });
                        return JsonDocument.Parse(json).RootElement.Clone();
                    }
                }
            }

            // no match or unsupported rule shape — provide worker-specific fallbacks
            if (string.Equals(worker, "validation", StringComparison.OrdinalIgnoreCase))
            {
                return JsonDocument.Parse("{\"valid\":true}").RootElement.Clone();
            }

            if (string.Equals(worker, "exemption", StringComparison.OrdinalIgnoreCase))
            {
                return JsonDocument.Parse("{\"exempt\":false}").RootElement.Clone();
            }

            if (string.Equals(worker, "applicability", StringComparison.OrdinalIgnoreCase))
            {
                return JsonDocument.Parse("{\"applicable\":false}").RootElement.Clone();
            }

            // generic fallback
            return JsonDocument.Parse("{\"matched\":null}").RootElement.Clone();
        }
    }
}
