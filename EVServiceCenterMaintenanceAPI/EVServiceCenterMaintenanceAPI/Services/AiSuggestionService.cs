using EVServiceCenterMaintenanceAPI.DTO;

namespace EVServiceCenterMaintenanceAPI.Services
{
    public class AiSuggestionService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AiSuggestionService> _logger;
        private readonly List<string> _geminiModels = new() { "gemini-2.0-flash", "gemini-2.5-flash" };
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
    }
}
