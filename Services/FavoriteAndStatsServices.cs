using Microsoft.EntityFrameworkCore;
using MovieReviewPlatform.Data;
using MovieReviewPlatform.Models;
using MovieReviewPlatform.ViewModels;

namespace MovieReviewPlatform.Services;

public interface IFavoriteService
{
    /// <summary>Dodaje ili uklanja film iz favorita. Vraca novo stanje (true znaci u favoritima).</summary>
    Task<bool> ToggleAsync(int userId, int movieId);
    Task<bool> IsFavoriteAsync(int userId, int movieId);
}

public class FavoriteService : IFavoriteService
{
    private readonly ApplicationDbContext _db;

    public FavoriteService(ApplicationDbContext db) => _db = db;

    public async Task<bool> ToggleAsync(int userId, int movieId)
    {
        var existing = await _db.Favorites
            .FirstOrDefaultAsync(f => f.UserId == userId && f.MovieId == movieId);

        if (existing is not null)
        {
            _db.Favorites.Remove(existing);
            await _db.SaveChangesAsync();
            return false;
        }

        if (!await _db.Movies.AnyAsync(m => m.Id == movieId)) return false;

        _db.Favorites.Add(new Favorite { UserId = userId, MovieId = movieId, CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsFavoriteAsync(int userId, int movieId) =>
        await _db.Favorites.AnyAsync(f => f.UserId == userId && f.MovieId == movieId);
}

public interface IStatisticsService
{
    Task<RankingsViewModel> GetRankingsAsync();
    Task<AdminStatsViewModel> GetAdminStatsAsync();
}

public class StatisticsService : IStatisticsService
{
    private readonly ApplicationDbContext _db;

    public StatisticsService(ApplicationDbContext db) => _db = db;

    public async Task<RankingsViewModel> GetRankingsAsync()
    {
        var movies = await _db.Movies
            .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
            .Include(m => m.Reviews)
            .Include(m => m.Favorites)
            .ToListAsync();

        static double Avg(Movie m) => m.Reviews.Count == 0 ? 0 : m.Reviews.Average(r => r.Rating);
        static double Pop(Movie m) => m.Views + m.Reviews.Count * 50 + m.Favorites.Count * 30;

        var bestRated = movies
            .Where(m => Avg(m) > 0)
            .OrderByDescending(Avg)
            .Take(10)
            .Select((m, i) => new RankingRowViewModel
            {
                Rank = i + 1,
                MovieId = m.Id,
                Title = m.Title,
                MetaText = $"{m.Year} · {m.GenresText}",
                ValueText = Avg(m).ToString("0.0")
            }).ToList();

        var mostPopular = movies
            .OrderByDescending(Pop)
            .Take(10)
            .Select((m, i) => new RankingRowViewModel
            {
                Rank = i + 1,
                MovieId = m.Id,
                Title = m.Title,
                MetaText = $"{m.Year} · {m.GenresText}",
                ValueText = $"{m.Views:N0} views"
            }).ToList();

        var reviewers = await _db.Users
            .Include(u => u.Reviews)
            .ToListAsync();

        var topReviewers = reviewers
            .Where(u => u.Reviews.Count > 0)
            .OrderByDescending(u => u.Reviews.Count)
            .Take(10)
            .Select((u, i) => new TopReviewerViewModel
            {
                Rank = i + 1,
                Name = u.Name,
                Initials = u.Initials(),
                ReviewCount = u.Reviews.Count
            }).ToList();

        return new RankingsViewModel
        {
            BestRated = bestRated,
            MostPopular = mostPopular,
            TopReviewers = topReviewers
        };
    }

    public async Task<AdminStatsViewModel> GetAdminStatsAsync()
    {
        var totalReviews = await _db.Reviews.CountAsync();

        return new AdminStatsViewModel
        {
            TotalUsers = await _db.Users.CountAsync(),
            TotalMovies = await _db.Movies.CountAsync(),
            TotalReviews = totalReviews,
            TotalComments = await _db.Comments.CountAsync(),
            AverageRating = totalReviews == 0 ? 0 : await _db.Reviews.AverageAsync(r => (double)r.Rating)
        };
    }
}
