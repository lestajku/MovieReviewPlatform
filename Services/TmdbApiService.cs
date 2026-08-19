using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MovieReviewPlatform.Services;

public record TmdbMovieSummary(int TmdbId, string Title, int? Year, string? PosterUrl, double Rating);

public record TmdbMovieDetails(
    int TmdbId,
    string Title,
    string Overview,
    double Rating,
    string? PosterUrl,
    int? Year,
    int? RuntimeMinutes,
    string? Director,
    string? Cast,
    List<string> Genres);

public interface ITmdbApiService
{
    Task<List<TmdbMovieSummary>> SearchAsync(string query, CancellationToken ct = default);
    Task<List<TmdbMovieSummary>> GetPopularAsync(int page, CancellationToken ct = default);
    Task<TmdbMovieDetails?> GetDetailsAsync(int tmdbId, CancellationToken ct = default);
}

public class TmdbApiService : ITmdbApiService
{
    private const string PosterBaseUrl = "https://image.tmdb.org/t/p/w500";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<TmdbApiService> _logger;

    public TmdbApiService(HttpClient http, IConfiguration config, ILogger<TmdbApiService> logger)
    {
        _http = http;
        _apiKey = config["Tmdb:ApiKey"] ?? throw new InvalidOperationException("Tmdb:ApiKey nije postavljen u konfiguraciji.");
        _logger = logger;
    }

    public async Task<List<TmdbMovieSummary>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<TmdbMovieSummary>();

        var url = $"search/movie?api_key={_apiKey}&query={Uri.EscapeDataString(query)}&include_adult=false";
        var response = await GetAsync<TmdbSearchResponseDto>(url, ct);
        if (response is null) return new List<TmdbMovieSummary>();

        return response.Results.Select(ToSummary).ToList();
    }

    public async Task<List<TmdbMovieSummary>> GetPopularAsync(int page, CancellationToken ct = default)
    {
        var url = $"movie/popular?api_key={_apiKey}&page={page}";
        var response = await GetAsync<TmdbSearchResponseDto>(url, ct);
        if (response is null) return new List<TmdbMovieSummary>();

        return response.Results.Select(ToSummary).ToList();
    }

    public async Task<TmdbMovieDetails?> GetDetailsAsync(int tmdbId, CancellationToken ct = default)
    {
        var url = $"movie/{tmdbId}?api_key={_apiKey}&append_to_response=credits";
        var dto = await GetAsync<TmdbMovieDto>(url, ct);
        if (dto is null) return null;

        var director = dto.Credits?.Crew?.FirstOrDefault(c => c.Job == "Director")?.Name;
        var cast = dto.Credits?.Cast?.Take(5).Select(c => c.Name).ToList() ?? new List<string>();

        return new TmdbMovieDetails(
            TmdbId: dto.Id,
            Title: dto.Title,
            Overview: dto.Overview ?? string.Empty,
            Rating: dto.VoteAverage,
            PosterUrl: ToPosterUrl(dto.PosterPath),
            Year: ParseYear(dto.ReleaseDate),
            RuntimeMinutes: dto.Runtime,
            Director: director,
            Cast: cast.Count == 0 ? null : string.Join(", ", cast),
            Genres: dto.Genres?.Select(g => g.Name).ToList() ?? new List<string>());
    }

    private static TmdbMovieSummary ToSummary(TmdbMovieDto dto) => new(
        TmdbId: dto.Id,
        Title: dto.Title,
        Year: ParseYear(dto.ReleaseDate),
        PosterUrl: ToPosterUrl(dto.PosterPath),
        Rating: dto.VoteAverage);

    private static string? ToPosterUrl(string? posterPath) =>
        string.IsNullOrEmpty(posterPath) ? null : PosterBaseUrl + posterPath;

    private static int? ParseYear(string? releaseDate) =>
        !string.IsNullOrEmpty(releaseDate) && releaseDate.Length >= 4 && int.TryParse(releaseDate[..4], out var y) ? y : null;

    private async Task<T?> GetAsync<T>(string relativeUrl, CancellationToken ct) where T : class
    {
        try
        {
            using var response = await _http.GetAsync(relativeUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("TMDB API returned {Status} for {Url}", response.StatusCode, relativeUrl);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "TMDB API call failed for {Url}", relativeUrl);
            return null;
        }
    }

    private sealed class TmdbSearchResponseDto
    {
        [JsonPropertyName("results")]
        public List<TmdbMovieDto> Results { get; set; } = new();
    }

    private sealed class TmdbMovieDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("overview")] public string? Overview { get; set; }
        [JsonPropertyName("release_date")] public string? ReleaseDate { get; set; }
        [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }
        [JsonPropertyName("vote_average")] public double VoteAverage { get; set; }
        [JsonPropertyName("runtime")] public int? Runtime { get; set; }
        [JsonPropertyName("genres")] public List<TmdbGenreDto>? Genres { get; set; }
        [JsonPropertyName("credits")] public TmdbCreditsDto? Credits { get; set; }
    }

    private sealed class TmdbGenreDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    }

    private sealed class TmdbCreditsDto
    {
        [JsonPropertyName("cast")] public List<TmdbCastDto>? Cast { get; set; }
        [JsonPropertyName("crew")] public List<TmdbCrewDto>? Crew { get; set; }
    }

    private sealed class TmdbCastDto
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    }

    private sealed class TmdbCrewDto
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("job")] public string Job { get; set; } = string.Empty;
    }
}
