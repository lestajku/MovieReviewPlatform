using Microsoft.EntityFrameworkCore;
using MovieReviewPlatform.Data;
using MovieReviewPlatform.Models;
using MovieReviewPlatform.ViewModels;

namespace MovieReviewPlatform.Services;

public interface IMovieService
{
    Task<HomeViewModel> GetHomeAsync(int? userId);
    Task<CatalogViewModel> GetCatalogAsync(MovieFilterViewModel filter, int? userId);
    Task<MovieDetailsViewModel?> GetDetailsAsync(int movieId, int? userId);
    Task RegisterViewAsync(int movieId);
    Task<List<string>> GetGenreNamesAsync();

    Task<List<AdminMovieRowViewModel>> GetAdminRowsAsync();
    Task<MovieFormViewModel?> GetFormAsync(int movieId);
    Task<int> CreateAsync(MovieFormViewModel form);
    Task<bool> UpdateAsync(MovieFormViewModel form);
    Task<bool> DeleteAsync(int movieId);
}

public class MovieService : IMovieService
{
    private const string PosterUploadFolder = "uploads/posters";

    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;

    public MovieService(ApplicationDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // pomocne metode

    private IQueryable<Movie> MoviesFull() => _db.Movies
        .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
        .Include(m => m.Reviews)
        .Include(m => m.Favorites);

    private static double Average(Movie m) => m.Reviews.Count > 0 ? m.Reviews.Average(r => r.Rating) : m.ExternalRating ?? 0;

    /// <summary>Popularnost: pregledi + 50 po recenziji + 30 po favoritu (kao u prototipu).</summary>
    private static double Popularity(Movie m) => m.Views + m.Reviews.Count * 50 + m.Favorites.Count * 30;

    private async Task<HashSet<int>> FavoriteIdsAsync(int? userId)
    {
        if (userId is null) return new HashSet<int>();
        var ids = await _db.Favorites
            .Where(f => f.UserId == userId)
            .Select(f => f.MovieId)
            .ToListAsync();
        return ids.ToHashSet();
    }

    private static MovieCardViewModel Card(Movie m, HashSet<int> favIds) => new()
    {
        Id = m.Id,
        Title = m.Title,
        Year = m.Year,
        GenresText = m.GenresText,
        PosterUrl = m.PosterUrl,
        AverageRating = Average(m),
        ReviewCount = m.Reviews.Count,
        IsFavorite = favIds.Contains(m.Id)
    };

    private static bool YearInRange(int year, string? range) => range switch
    {
        "2020s" => year >= 2020,
        "2010s" => year >= 2010 && year <= 2019,
        "2000s" => year >= 2000 && year <= 2009,
        "pre2000" => year < 2000,
        _ => true
    };

    private static List<string> SplitList(string? value) => (value ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    // naslovnica

    public async Task<HomeViewModel> GetHomeAsync(int? userId)
    {
        var movies = await MoviesFull().ToListAsync();
        var favIds = await FavoriteIdsAsync(userId);
        var used = new HashSet<int>();

        var trending = movies.OrderByDescending(Popularity).Take(6).ToList();
        used.UnionWith(trending.Select(m => m.Id));

        var newest = movies.Where(m => !used.Contains(m.Id))
            .OrderByDescending(m => m.AddedAt).Take(4).ToList();
        used.UnionWith(newest.Select(m => m.Id));

        var topRated = movies.Where(m => !used.Contains(m.Id) && Average(m) > 0)
            .OrderByDescending(Average).Take(4).ToList();
        used.UnionWith(topRated.Select(m => m.Id));

        var recommended = userId is null
            ? new List<Movie>()
            : Recommend(movies, await RatedByUserAsync(userId.Value), await LikedGenresAsync(userId.Value), used, 4);

        return new HomeViewModel
        {
            Trending = trending.Select(m => Card(m, favIds)).ToList(),
            Newest = newest.Select(m => Card(m, favIds)).ToList(),
            TopRated = topRated.Select(m => Card(m, favIds)).ToList(),
            Recommended = recommended.Select(m => Card(m, favIds)).ToList()
        };
    }

    private async Task<HashSet<int>> RatedByUserAsync(int userId)
    {
        var ids = await _db.Reviews.Where(r => r.UserId == userId).Select(r => r.MovieId).ToListAsync();
        return ids.ToHashSet();
    }

    /// <summary>Broji koliko je puta korisnik dobro ocijenio (ocjena 7 ili vise) film pojedinog zanra.</summary>
    private async Task<Dictionary<string, int>> LikedGenresAsync(int userId)
    {
        var genreNames = await _db.Reviews
            .Where(r => r.UserId == userId && r.Rating >= 7)
            .SelectMany(r => r.Movie.MovieGenres.Select(mg => mg.Genre.Name))
            .ToListAsync();

        return genreNames
            .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
    }

    private static List<Movie> Recommend(
        List<Movie> pool, HashSet<int> alreadyRated, Dictionary<string, int> likedGenres,
        HashSet<int> exclude, int take)
    {
        return pool
            .Where(m => !alreadyRated.Contains(m.Id) && !exclude.Contains(m.Id))
            .Select(m => new
            {
                Movie = m,
                Score = m.GenreNames.Sum(g => likedGenres.TryGetValue(g, out var c) ? c : 0) + Average(m) / 10
            })
            .OrderByDescending(x => x.Score)
            .Take(take)
            .Select(x => x.Movie)
            .ToList();
    }

    // katalog

    public async Task<CatalogViewModel> GetCatalogAsync(MovieFilterViewModel filter, int? userId)
    {
        var movies = await MoviesFull().ToListAsync();
        var favIds = await FavoriteIdsAsync(userId);
        var q = (filter.Q ?? string.Empty).Trim();

        var filtered = movies.Where(m =>
        {
            if (q.Length > 0 && !m.Title.Contains(q, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.IsNullOrEmpty(filter.Genre) &&
                !m.GenreNames.Contains(filter.Genre, StringComparer.OrdinalIgnoreCase)) return false;
            if (!YearInRange(m.Year, filter.YearRange)) return false;
            if (filter.MinRating > 0 && Average(m) < filter.MinRating) return false;
            return true;
        });

        filtered = filter.Sort switch
        {
            "newest" => filtered.OrderByDescending(m => m.AddedAt),
            "rating" => filtered.OrderByDescending(Average),
            "title" => filtered.OrderBy(m => m.Title, StringComparer.CurrentCultureIgnoreCase),
            "year" => filtered.OrderByDescending(m => m.Year),
            _ => filtered.OrderByDescending(Popularity)
        };

        return new CatalogViewModel
        {
            Filter = filter,
            Movies = filtered.Select(m => Card(m, favIds)).ToList(),
            Genres = await GetGenreNamesAsync()
        };
    }

    public async Task<List<string>> GetGenreNamesAsync() =>
        await _db.Genres.OrderBy(g => g.Name).Select(g => g.Name).ToListAsync();

    // detalji

    public async Task<MovieDetailsViewModel?> GetDetailsAsync(int movieId, int? userId)
    {
        var movie = await _db.Movies
            .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
            .Include(m => m.Favorites)
            .Include(m => m.Reviews).ThenInclude(r => r.User)
            .Include(m => m.Reviews).ThenInclude(r => r.Comments).ThenInclude(c => c.User)
            .FirstOrDefaultAsync(m => m.Id == movieId);

        if (movie is null) return null;

        var isAdmin = userId is not null &&
                      await _db.Users.AnyAsync(u => u.Id == userId && u.Role == UserRole.Admin);

        var ownReview = userId is null ? null : movie.Reviews.FirstOrDefault(r => r.UserId == userId);

        var vm = new MovieDetailsViewModel
        {
            Id = movie.Id,
            Title = movie.Title,
            Year = movie.Year,
            Duration = movie.Duration,
            Director = movie.Director,
            Cast = movie.Cast,
            Description = movie.Description,
            PosterUrl = movie.PosterUrl,
            Genres = movie.GenreNames.ToList(),
            AverageRating = Average(movie),
            ReviewCount = movie.Reviews.Count,
            IsFavorite = userId is not null && movie.Favorites.Any(f => f.UserId == userId),
            HasOwnReview = ownReview is not null,
            ReviewForm = new ReviewFormViewModel
            {
                MovieId = movie.Id,
                Rating = ownReview?.Rating ?? 0,
                Text = ownReview?.Text ?? string.Empty
            },
            Reviews = movie.Reviews
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReviewViewModel
                {
                    Id = r.Id,
                    MovieId = movie.Id,
                    MovieTitle = movie.Title,
                    AuthorName = r.User?.Name ?? "Deleted user",
                    AuthorInitials = r.User?.Initials() ?? "?",
                    Rating = r.Rating,
                    Text = r.Text,
                    CreatedAt = r.CreatedAt,
                    IsMine = userId is not null && r.UserId == userId,
                    CanModerate = isAdmin,
                    Comments = r.Comments
                        .OrderBy(c => c.CreatedAt)
                        .Select(c => new CommentViewModel
                        {
                            Id = c.Id,
                            AuthorName = c.User?.Name ?? "Deleted user",
                            Text = c.Text,
                            CreatedAt = c.CreatedAt,
                            CanDelete = isAdmin || (userId is not null && c.UserId == userId)
                        }).ToList()
                }).ToList()
        };

        vm.Similar = await GetSimilarAsync(movie, userId);
        return vm;
    }

    private async Task<List<MovieCardViewModel>> GetSimilarAsync(Movie baseMovie, int? userId)
    {
        var others = await MoviesFull().Where(m => m.Id != baseMovie.Id).ToListAsync();
        var favIds = await FavoriteIdsAsync(userId);
        var baseGenres = baseMovie.GenreNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return others
            .Select(m => new
            {
                Movie = m,
                Score = m.GenreNames.Count(baseGenres.Contains) * 10 + Average(m)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(6)
            .Select(x => Card(x.Movie, favIds))
            .ToList();
    }

    public async Task RegisterViewAsync(int movieId)
    {
        var movie = await _db.Movies.FindAsync(movieId);
        if (movie is null) return;
        movie.Views++;
        await _db.SaveChangesAsync();
    }

    // admin CRUD

    public async Task<List<AdminMovieRowViewModel>> GetAdminRowsAsync()
    {
        var movies = await MoviesFull().ToListAsync();
        return movies
            .OrderBy(m => m.Title, StringComparer.CurrentCultureIgnoreCase)
            .Select(m => new AdminMovieRowViewModel
            {
                Id = m.Id,
                Title = m.Title,
                Year = m.Year,
                GenresText = m.GenresText,
                AverageRating = Average(m),
                ReviewCount = m.Reviews.Count
            }).ToList();
    }

    public async Task<MovieFormViewModel?> GetFormAsync(int movieId)
    {
        var m = await _db.Movies
            .Include(x => x.MovieGenres).ThenInclude(mg => mg.Genre)
            .FirstOrDefaultAsync(x => x.Id == movieId);

        if (m is null) return null;

        return new MovieFormViewModel
        {
            Id = m.Id,
            Title = m.Title,
            Year = m.Year,
            Duration = m.Duration,
            Genres = m.GenresText,
            Director = m.Director,
            Cast = m.Cast,
            Description = m.Description,
            PosterUrl = m.PosterUrl,
            CurrentPosterUrl = m.PosterUrl
        };
    }

    public async Task<int> CreateAsync(MovieFormViewModel form)
    {
        var posterUrl = await SavePosterFileAsync(form.PosterFile)
            ?? (string.IsNullOrWhiteSpace(form.PosterUrl) ? null : form.PosterUrl.Trim());

        var movie = new Movie
        {
            Title = form.Title.Trim(),
            Year = form.Year,
            Duration = form.Duration,
            Director = form.Director.Trim(),
            Cast = string.Join(", ", SplitList(form.Cast)),
            Description = form.Description?.Trim() ?? string.Empty,
            PosterUrl = posterUrl,
            AddedAt = DateTime.UtcNow,
            Views = 0
        };

        await ApplyGenresAsync(movie, form.Genres);
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync();
        return movie.Id;
    }

    public async Task<bool> UpdateAsync(MovieFormViewModel form)
    {
        var movie = await _db.Movies
            .Include(m => m.MovieGenres)
            .FirstOrDefaultAsync(m => m.Id == form.Id);

        if (movie is null) return false;

        movie.Title = form.Title.Trim();
        movie.Year = form.Year;
        movie.Duration = form.Duration;
        movie.Director = form.Director.Trim();
        movie.Cast = string.Join(", ", SplitList(form.Cast));
        movie.Description = form.Description?.Trim() ?? string.Empty;

        var uploadedPosterUrl = await SavePosterFileAsync(form.PosterFile);
        if (uploadedPosterUrl is not null)
        {
            DeleteLocalPosterFile(movie.PosterUrl);
            movie.PosterUrl = uploadedPosterUrl;
        }
        else
        {
            movie.PosterUrl = string.IsNullOrWhiteSpace(form.PosterUrl) ? null : form.PosterUrl.Trim();
        }

        _db.MovieGenres.RemoveRange(movie.MovieGenres);
        movie.MovieGenres.Clear();
        await ApplyGenresAsync(movie, form.Genres);

        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>Sprema upload na disk (wwwroot/uploads/posters) i vraca web relativni URL, ili null ako nista nije uploadano.</summary>
    private async Task<string?> SavePosterFileAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0) return null;

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var folderPath = Path.Combine(_env.WebRootPath, PosterUploadFolder);
        Directory.CreateDirectory(folderPath);

        var filePath = Path.Combine(folderPath, fileName);
        await using (var stream = File.Create(filePath))
        {
            await file.CopyToAsync(stream);
        }

        return $"/{PosterUploadFolder}/{fileName}";
    }

    /// <summary>Brise stari upload s diska kad se zamijeni novim (ne dira vanjske URLove).</summary>
    private void DeleteLocalPosterFile(string? posterUrl)
    {
        if (string.IsNullOrEmpty(posterUrl) || !posterUrl.StartsWith($"/{PosterUploadFolder}/", StringComparison.OrdinalIgnoreCase))
            return;

        var filePath = Path.Combine(_env.WebRootPath, posterUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        try
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
        catch (IOException)
        {
            // najbolji pokusaj, ne blokira spremanje filma ako se datoteka ne moze obrisati
        }
    }

    /// <summary>Pretvara "Drama, Thriller" u Genre entitete; nepostojece zanrove kreira.</summary>
    private async Task ApplyGenresAsync(Movie movie, string genresCsv)
    {
        var names = SplitList(genresCsv);
        if (names.Count == 0) return;

        var existing = await _db.Genres.ToListAsync();

        foreach (var name in names)
        {
            var genre = existing.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
            if (genre is null)
            {
                genre = new Genre { Name = name };
                _db.Genres.Add(genre);
                existing.Add(genre);
            }
            movie.MovieGenres.Add(new MovieGenre { Movie = movie, Genre = genre });
        }
    }

    public async Task<bool> DeleteAsync(int movieId)
    {
        var movie = await _db.Movies
            .Include(m => m.Reviews)
            .FirstOrDefaultAsync(m => m.Id == movieId);

        if (movie is null) return false;

        // komentari na recenzijama filma (FK prema korisniku je Restrict pa ih brisemo rucno)
        var reviewIds = movie.Reviews.Select(r => r.Id).ToList();
        var comments = await _db.Comments.Where(c => reviewIds.Contains(c.ReviewId)).ToListAsync();
        _db.Comments.RemoveRange(comments);

        DeleteLocalPosterFile(movie.PosterUrl);
        _db.Movies.Remove(movie);
        await _db.SaveChangesAsync();
        return true;
    }
}
