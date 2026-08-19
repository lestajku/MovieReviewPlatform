using Microsoft.EntityFrameworkCore;
using MovieReviewPlatform.Models;
using MovieReviewPlatform.Services;

namespace MovieReviewPlatform.Data;

/// <summary>
/// Jednokratno (pri prvom pokretanju) dopunjava katalog popularnim filmovima s TMDBa,
/// dok broj filmova u bazi ne dosegne <see cref="TargetMovieCount"/>. Nakon toga se preskace
/// na svakom sljedecem startu jer je broj filmova vec dosegnut, ne radi ponovne pozive uzalud.
/// Poziva se sekvencijalno s malim odmakom izmedu zahtjeva radi TMDB rate limita.
/// </summary>
public static class TmdbMovieImporter
{
    private const int TargetMovieCount = 118; // 18 seed + ~100 s TMDBa
    private const int RequestDelayMs = 150;
    private const int MaxPages = 10;

    public static async Task ImportPopularAsync(ApplicationDbContext db, ITmdbApiService tmdb, ILogger logger, CancellationToken ct = default)
    {
        var existingTitles = await db.Movies.Select(m => m.Title).ToListAsync(ct);
        var titles = new HashSet<string>(existingTitles, StringComparer.OrdinalIgnoreCase);

        if (titles.Count < TargetMovieCount)
        {
            var genreCache = await db.Genres.ToDictionaryAsync(g => g.Name, StringComparer.OrdinalIgnoreCase, ct);

            var imported = 0;
            for (var page = 1; page <= MaxPages && titles.Count < TargetMovieCount; page++)
            {
                var popular = await tmdb.GetPopularAsync(page, ct);
                await Task.Delay(RequestDelayMs, ct);
                if (popular.Count == 0) break;

                foreach (var summary in popular)
                {
                    if (titles.Count >= TargetMovieCount) break;
                    if (titles.Contains(summary.Title)) continue;

                    var details = await tmdb.GetDetailsAsync(summary.TmdbId, ct);
                    await Task.Delay(RequestDelayMs, ct);
                    if (details is null || string.IsNullOrWhiteSpace(details.Overview)) continue;
                    if (titles.Contains(details.Title)) continue;

                    var movie = new Movie
                    {
                        Title = details.Title,
                        Year = details.Year ?? DateTime.UtcNow.Year,
                        Duration = details.RuntimeMinutes is > 0 ? details.RuntimeMinutes.Value : 100,
                        Director = string.IsNullOrWhiteSpace(details.Director) ? "Unknown" : details.Director,
                        Cast = details.Cast ?? string.Empty,
                        Description = details.Overview,
                        PosterUrl = details.PosterUrl,
                        AddedAt = DateTime.UtcNow,
                        Views = 0,
                        ExternalRating = details.Rating > 0 ? details.Rating : null
                    };

                    foreach (var genreName in details.Genres)
                    {
                        if (!genreCache.TryGetValue(genreName, out var genre))
                        {
                            genre = new Genre { Name = genreName };
                            genreCache[genreName] = genre;
                            db.Genres.Add(genre);
                        }
                        movie.MovieGenres.Add(new MovieGenre { Movie = movie, Genre = genre });
                    }

                    db.Movies.Add(movie);
                    titles.Add(details.Title);
                    imported++;
                }
            }

            if (imported > 0)
            {
                await db.SaveChangesAsync(ct);
                logger.LogInformation("TMDB import: added {Count} movies (total now {Total}).", imported, titles.Count);
            }
        }

        await BackfillExternalRatingsAsync(db, tmdb, logger, ct);
    }

    /// <summary>
    /// Popunjava ExternalRating za filmove bez ijedne recenzije kojima jos nedostaje (npr. uvezeni
    /// prije nego je ovo polje uvedeno). Bez toga ostaju s prosjekom 0 i ne prolaze filter minimalnog ratinga.
    /// </summary>
    private static async Task BackfillExternalRatingsAsync(ApplicationDbContext db, ITmdbApiService tmdb, ILogger logger, CancellationToken ct)
    {
        var missing = await db.Movies
            .Where(m => m.ExternalRating == null && !m.Reviews.Any())
            .ToListAsync(ct);

        if (missing.Count == 0) return;

        var updated = 0;
        foreach (var movie in missing)
        {
            var results = await tmdb.SearchAsync(movie.Title, ct);
            await Task.Delay(RequestDelayMs, ct);

            var match = results.FirstOrDefault(r => string.Equals(r.Title, movie.Title, StringComparison.OrdinalIgnoreCase))
                        ?? results.FirstOrDefault();

            if (match is not null && match.Rating > 0)
            {
                movie.ExternalRating = match.Rating;
                updated++;
            }
        }

        if (updated > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("TMDB backfill: set ExternalRating for {Count} movies.", updated);
        }
    }
}
