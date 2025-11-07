using System.Text;
using System.Text.Json;
using EVServiceCenterMaintenanceAPI.DTO;
using static EVServiceCenterMaintenanceAPI.DTO.AiSuggestionDto;

namespace EVServiceCenterMaintenanceAPI.Services
{
    public class AiSuggestionService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AiSuggestionService> _logger;
        private readonly List<string> _geminiModels = ["gemini-2.0-flash", "gemini-2.5-flash"];
        private readonly List<GeminiApiKey> _apiKeys;

        public AiSuggestionService(HttpClient httpClient, IConfiguration configuration, ILogger<AiSuggestionService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _apiKeys = _configuration.GetSection("Gemini:ApiKeys").Get<List<GeminiApiKey>>() ?? new List<GeminiApiKey>();
            if (_apiKeys.Count == 0)
                throw new InvalidOperationException("No Gemini API keys configured.");
        }

        public async Task<List<PartAiSuggestionDto>> GetPartReorderSuggestionsAsync(int centerId, List<PartAiUsageHistoryDto> usageHistory)
        {
            if (usageHistory == null || !usageHistory.Any())
            {
                _logger.LogWarning("No usage history provided for AI suggestions for center {CenterId}.", centerId);
                return new List<PartAiSuggestionDto>();
            }

            var userData = new
            {
                CenterId = centerId,
                UsageHistory = usageHistory.Select(h => new
                {
                    h.PartId,
                    h.PartName,
                    h.QuantityUsed,
                    Date = h.Date.ToString("yyyy-MM-dd"),
                    h.Mileage
                }).ToList(),
                CurrentDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                Goal = "Suggest optimal minimum stock levels for EV parts to prevent shortages while minimizing inventory costs. Consider usage trends, lead times (7-14 days), safety stock (20% buffer), and classify usage trend as 'high' (>50 units/month), 'medium' (10-50 units/month), or 'low' (<10 units/month)."
            };

            var userDataJson = JsonSerializer.Serialize(userData, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            });

            var prompt =
                "Based on the provided usage history for an EV service center, suggest minimum stock levels for each part. " +
                "For each part, provide: PartId, PartName, CurrentUsageTrend ('high', 'medium', 'low'), SuggestedMinStock (integer), Reason (brief explanation). Output as a JSON array of objects. " +
                "Focus on data-driven suggestions considering frequency, volume, lead times, and safety stock.";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new object[]
                        {
                            new { text = prompt },
                            new { text = userDataJson }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.3,
                    topP = 0.8,
                    topK = 40,
                    maxOutputTokens = 1024,
                    responseMimeType = "application/json"
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = false
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var responseText = await CallGeminiWithAutoFailover(async (apiKey, model) =>
                {
                    var requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey.Key}";
                    var response = await _httpClient.PostAsync(requestUrl, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Gemini API error for center {CenterId}: {StatusCode} - {ErrorContent}", centerId, response.StatusCode, errorContent);
                        throw new HttpRequestException($"Gemini API error: {response.StatusCode} - {errorContent}");
                    }

                    var responseContent = await response.Content.ReadAsStringAsync();
                    var responseJson = JsonSerializer.Deserialize<JsonElement>(responseContent);

                    if (responseJson.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                    {
                        var candidate = candidates[0];
                        if (candidate.TryGetProperty("content", out var contentProp) && contentProp.TryGetProperty("parts", out var parts))
                        {
                            if (parts.GetArrayLength() > 0 && parts[0].TryGetProperty("text", out var textProp))
                            {
                                return textProp.GetString() ?? string.Empty;
                            }
                        }
                    }

                    throw new InvalidOperationException("Unexpected Gemini API response format.");
                });

                // Parse AI response
                var suggestionsJson = responseText?.Trim().Trim('[', ']');
                if (string.IsNullOrEmpty(suggestionsJson) || suggestionsJson == "[]")
                {
                    _logger.LogWarning("AI returned no suggestions for center {CenterId}.", centerId);
                    return new List<PartAiSuggestionDto>();
                }

                var suggestions = JsonSerializer.Deserialize<List<PartAiSuggestionDto>>(suggestionsJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<PartAiSuggestionDto>();

                // Post-process suggestions
                foreach (var suggestion in suggestions)
                {
                    suggestion.SuggestedMinStock = Math.Max(suggestion.SuggestedMinStock, 0);
                    suggestion.Reason = suggestion.Reason?.Trim() ?? "AI-based calculation from usage trends.";
                    suggestion.CurrentUsageTrend = suggestion.CurrentUsageTrend?.ToLower() switch
                    {
                        "high" => "high",
                        "medium" => "medium",
                        "low" => "low",
                        _ => "medium" // Default if invalid
                    };
                }

                _logger.LogInformation("Generated {Count} AI part suggestions for center {CenterId}.", suggestions.Count, centerId);
                return suggestions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate AI suggestions for center {CenterId}. Falling back to basic calculation.", centerId);
                return await GetFallbackPartSuggestionsAsync(usageHistory, centerId);
            }
        }

        private async Task<List<PartAiSuggestionDto>> GetFallbackPartSuggestionsAsync(List<PartAiUsageHistoryDto> usageHistory, int centerId)
        {
            return await Task.Run(() =>
            {
                var suggestions = new List<PartAiSuggestionDto>();
                var oneMonthAgo = DateTime.UtcNow.AddMonths(-1);

                var groupedUsage = usageHistory
                    .Where(u => u.Date >= oneMonthAgo) // Consider last 30 days
                    .GroupBy(u => u.PartId)
                    .Select(g => new
                    {
                        PartId = g.Key,
                        PartName = usageHistory.First(u => u.PartId == g.Key).PartName,
                        AvgMonthlyUsage = g.Average(u => u.QuantityUsed),
                        TotalUsed = g.Sum(u => u.QuantityUsed)
                    });

                foreach (var group in groupedUsage)
                {
                    // Determine usage trend based on total usage in the last month
                    string usageTrend = group.TotalUsed switch
                    {
                        > 50 => "high",
                        >= 10 => "medium",
                        _ => "low"
                    };

                    // Calculate suggested stock: 1.5x average monthly usage + 20% safety stock
                    int suggestedMinStock = (int)Math.Ceiling(group.AvgMonthlyUsage * 1.5 * 1.2);

                    suggestions.Add(new PartAiSuggestionDto
                    {
                        PartId = group.PartId,
                        PartName = group.PartName,
                        CurrentUsageTrend = usageTrend,
                        SuggestedMinStock = suggestedMinStock,
                        Reason = $"Fallback calculation: Based on {group.TotalUsed} units used in the last month."
                    });
                }

                _logger.LogInformation("Generated {Count} fallback part suggestions for center {CenterId}.", suggestions.Count, centerId);
                return suggestions;
            });
        }


        private async Task<string> CallGeminiWithAutoFailover(Func<GeminiApiKey, string, Task<string>> apiCall)
        {
            var exceptions = new List<Exception>();
            foreach (var model in _geminiModels)
            {
                foreach (var apiKey in _apiKeys)
                {
                    try
                    {
                        return await apiCall(apiKey, model);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                        await Task.Delay(1000);
                    }
                }
            }

            throw new AggregateException("All Gemini API calls failed.", exceptions);
        }

    }
}
