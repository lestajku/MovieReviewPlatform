using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieReviewPlatform.Services;
using MovieReviewPlatform.ViewModels;

namespace MovieReviewPlatform.Controllers;

public class HomeController : BaseController
{
    private readonly IMovieService _movies;

    public HomeController(IMovieService movies) => _movies = movies;

    public async Task<IActionResult> Index()
    {
        var vm = await _movies.GetHomeAsync(CurrentUserId);
        return View(vm);
    }

    [ResponseCache(Duration = 0, NoStore = true)]
    public IActionResult Error() => View();
}

public class MoviesController : BaseController
{
    private readonly IMovieService _movies;

    public MoviesController(IMovieService movies) => _movies = movies;

    /// Katalog s pretragom, filtriranjem i sortiranjem.
    public async Task<IActionResult> Index([FromQuery] MovieFilterViewModel filter)
    {
        var vm = await _movies.GetCatalogAsync(filter, CurrentUserId);
        return View(vm);
    }

    public async Task<IActionResult> Details(int id)
    {
        var vm = await _movies.GetDetailsAsync(id, CurrentUserId);
        if (vm is null) return NotFound();

        await _movies.RegisterViewAsync(id);
        return View(vm);
    }
}

public class RankingsController : BaseController
{
    private readonly IStatisticsService _stats;

    public RankingsController(IStatisticsService stats) => _stats = stats;

    public async Task<IActionResult> Index()
    {
        var vm = await _stats.GetRankingsAsync();
        return View(vm);
    }
}

[Authorize]
public class FavoritesController : BaseController
{
    private readonly IFavoriteService _favorites;

    public FavoritesController(IFavoriteService favorites) => _favorites = favorites;

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int movieId)
    {
        var userId = CurrentUserId;
        if (userId is null) return Challenge();

        var isFavorite = await _favorites.ToggleAsync(userId.Value, movieId);
        Notify(isFavorite ? "Added to favorites." : "Removed from favorites.");

        return RedirectBack("Details", "Movies");
    }
}
