using System.Diagnostics;
using System.Text;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using VatIT.Domain.Entities;

namespace VatIT.Orchestrator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BenchmarkController : ControllerBase
{
    private readonly IHttpClientFactory _httpFactory;

    public BenchmarkController(IHttpClientFactory httpFactory)
    {
        _httpFactory = httpFactory;
    }

    /// <summary>
    /// Run a simple benchmark by sending a number of POST requests to a target URL using a small set of sample payloads.
    /// </summary>
    /// <param name="targetUrl">The URL to POST to (defaults to local orchestrator /api/transaction/process).</param>
    /// <param name="totalRequests">Total number of requests to send (default 10000).</param>
    /// <param name="concurrency">Max concurrent requests (default 200).</param>
    [HttpPost("run")]
    public async Task<IActionResult> RunBenchmark(
        [FromQuery] string? targetUrl = null,
        [FromQuery] int totalRequests = 10000,
        [FromQuery] int concurrency = 200,
        CancellationToken cancellationToken = default)
    {
        targetUrl ??= $"http://localhost:5100/api/transaction/process";

        if (totalRequests <= 0) totalRequests = 10000;
        if (concurrency <= 0) concurrency = 200;

        var client = _httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        // Prepare 15 sample payloads
        var samples = CreateSamplePayloads(15);

        var stopwatch = Stopwatch.StartNew();

        int success = 0;
        int failed = 0;
        var firstErrors = new List<string>();
        var durations = new System.Collections.Concurrent.ConcurrentBag<double>();
        var workerDurations = new System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentBag<double>>();

        using var semaphore = new SemaphoreSlim(concurrency);

        var tasks = new List<Task>(totalRequests);

        for (int i = 0; i < totalRequests; i++)
        {
            await semaphore.WaitAsync(cancellationToken);

            // pick a sample and give it a fresh TransactionId
            var sample = CloneWithNewId(samples[i % samples.Count]);

            // No forced-fail manipulation here; benchmark uses the generated sample MerchantId values.
            var json = JsonSerializer.Serialize(sample, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var task = Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var resp = await client.PostAsync(targetUrl, content, cancellationToken);
                    var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                    sw.Stop();
                    durations.Add(sw.Elapsed.TotalMilliseconds);

                    // try to extract worker timings from the orchestration response (workerTimings)
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(body))
                        {
                            using var doc = JsonDocument.Parse(body);
                            if (doc.RootElement.TryGetProperty("workerTimings", out var timingsElement) && timingsElement.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var prop in timingsElement.EnumerateObject())
                                {
                                    var name = prop.Name;
                                    if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDouble(out var ms))
                                    {
                                        var bag = workerDurations.GetOrAdd(name, _ => new System.Collections.Concurrent.ConcurrentBag<double>());
                                        bag.Add(ms);
                                    }
                                }
                            }
                        }
                    }
                    catch { /* ignore parse errors */ }

                    if (resp.IsSuccessStatusCode)
                    {
                        Interlocked.Increment(ref success);
                    }
                    else
                    {
                        Interlocked.Increment(ref failed);
                        if (firstErrors.Count < 10)
                        {
                            lock (firstErrors) { firstErrors.Add($"{(int)resp.StatusCode}: {body}"); }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failed);
                    sw.Stop();
                    durations.Add(sw.Elapsed.TotalMilliseconds);
                    if (firstErrors.Count < 10)
                    {
                        lock (firstErrors) { firstErrors.Add(ex.Message); }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        stopwatch.Stop();

        // Compute duration statistics
        var durationArray = durations.ToArray();
        double avgMs = durationArray.Length > 0 ? Math.Round(durationArray.Average(), 2) : 0;
        double minMs = durationArray.Length > 0 ? Math.Round(durationArray.Min(), 2) : 0;
        double maxMs = durationArray.Length > 0 ? Math.Round(durationArray.Max(), 2) : 0;

        // Compute per-worker aggregate stats
        var workerStats = workerDurations.ToDictionary(kvp => kvp.Key, kvp =>
        {
            var arr = kvp.Value.ToArray();
            return new
            {
                Count = arr.Length,
                AvgMs = arr.Length > 0 ? Math.Round(arr.Average(), 2) : 0,
                MinMs = arr.Length > 0 ? Math.Round(arr.Min(), 2) : 0,
                MaxMs = arr.Length > 0 ? Math.Round(arr.Max(), 2) : 0
            };
        });

        var result = new
        {
            Target = targetUrl,
            TotalRequests = totalRequests,
            Concurrency = concurrency,
            Success = success,
            Failed = failed,
            DurationMs = stopwatch.ElapsedMilliseconds,
            AvgRequestMs = avgMs,
            FastestRequestMs = minMs,
            SlowestRequestMs = maxMs,
            WorkerStats = workerStats,
            ThroughputPerSec = Math.Round((double)totalRequests / Math.Max(1, stopwatch.Elapsed.TotalSeconds), 2),
            SampleErrors = firstErrors
        };

        return Ok(result);
    }

    private static List<TransactionRequest> CreateSamplePayloads(int count)
    {
        var list = new List<TransactionRequest>(count);
        for (int i = 0; i < count; i++)
        {
                list.Add(new TransactionRequest
            {
                TransactionId = Guid.NewGuid().ToString(),
                CustomerId = $"CUST-{i + 1}",
                MerchantId = $"MER-{(i % 5) + 1}",
                Destination = new Destination
                {
                    Country = "US",
                    State = (i % 2 == 0) ? "CA" : "NY",
                    City = (i % 2 == 0) ? "Los Angeles" : "New York"
                },
                    Currency = "USD",
                Items = new List<Item>
                {
                    new Item { Id = "item-1", Category = "electronics", Amount = 199.99m },
                    new Item { Id = "item-2", Category = "books", Amount = 29.99m }
                },
                TotalAmount = 229.98m
            });
        }
        return list;
    }

    private static TransactionRequest CloneWithNewId(TransactionRequest src)
    {
        return new TransactionRequest
        {
            TransactionId = Guid.NewGuid().ToString(),
            CustomerId = src.CustomerId,
            MerchantId = src.MerchantId,
            Destination = new Destination
            {
                Country = src.Destination.Country,
                State = src.Destination.State,
                City = src.Destination.City
            },
            Currency = src.Currency,
            Items = src.Items.Select(i => new Item { Id = i.Id, Category = i.Category, Amount = i.Amount }).ToList(),
            TotalAmount = src.TotalAmount
        };
    }
}
