using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieReviewPlatform.Services;
using MovieReviewPlatform.ViewModels;

namespace MovieReviewPlatform.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminController : BaseController
{
    private readonly IMovieService _movies;
    private readonly IReviewService _reviews;
    private readonly IUserService _users;
    private readonly IStatisticsService _stats;

    public AdminController(
        IMovieService movies,
        IReviewService reviews,
        IUserService users,
        IStatisticsService stats)
    {
        _movies = movies;
        _reviews = reviews;
        _users = users;
        _stats = stats;
    }

    /// Jedan pogled s karticama; ucitava se samo podatak za aktivnu karticu.
    public async Task<IActionResult> Index(string tab = "movies")
    {
        var userId = CurrentUserId ?? 0;

        var vm = new AdminDashboardViewModel { Tab = tab };

        switch (tab)
        {
            case "users":
                vm.Users = await _users.GetAdminRowsAsync(userId);
                break;
            case "reviews":
                vm.Reviews = await _reviews.GetAllForAdminAsync();
                break;
            case "stats":
                vm.Stats = await _stats.GetAdminStatsAsync();
                break;
            default:
                vm.Tab = "movies";
                vm.Movies = await _movies.GetAdminRowsAsync();
                break;
        }

        return View(vm);
    }

    //  filmovi 

    [HttpGet]
    public IActionResult CreateMovie() => View("MovieForm", new MovieFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMovie(MovieFormViewModel form)
    {
        if (!ModelState.IsValid) return View("MovieForm", form);

        await _movies.CreateAsync(form);
        Notify($"\"{form.Title}\" added.");
        return RedirectToAction(nameof(Index), new { tab = "movies" });
    }

    [HttpGet]
    public async Task<IActionResult> EditMovie(int id)
    {
        var form = await _movies.GetFormAsync(id);
        if (form is null) return NotFound();

        return View("MovieForm", form);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditMovie(MovieFormViewModel form)
    {
        if (!ModelState.IsValid) return View("MovieForm", form);

        var updated = await _movies.UpdateAsync(form);
        if (!updated) return NotFound();

        Notify($"\"{form.Title}\" saved.");
        return RedirectToAction(nameof(Index), new { tab = "movies" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMovie(int id)
    {
        var deleted = await _movies.DeleteAsync(id);
        if (!deleted) return NotFound();

        Notify("Movie deleted, along with its reviews and favorites.");
        return RedirectToAction(nameof(Index), new { tab = "movies" });
    }

    // korisnici 

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleRole(int id)
    {
        var result = await _users.ToggleRoleAsync(id);
        if (!result.Succeeded) NotifyError(result.Error!);
        else Notify("Role updated.");

        return RedirectToAction(nameof(Index), new { tab = "users" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var result = await _users.DeleteAsync(id, CurrentUserId ?? 0);
        if (!result.Succeeded) NotifyError(result.Error!);
        else Notify("User deleted.");

        return RedirectToAction(nameof(Index), new { tab = "users" });
    }

    //  recenzije 

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReview(int id)
    {
        var deleted = await _reviews.DeleteAsync(id, CurrentUserId ?? 0, isAdmin: true);
        if (!deleted) return NotFound();

        Notify("Review deleted.");
        return RedirectToAction(nameof(Index), new { tab = "reviews" });
    }
}
