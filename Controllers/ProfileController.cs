using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieReviewPlatform.Services;
using MovieReviewPlatform.ViewModels;

namespace MovieReviewPlatform.Controllers;

[Authorize]
public class ProfileController : BaseController
{
    private readonly IUserService _users;

    public ProfileController(IUserService users) => _users = users;

    public async Task<IActionResult> Index(string tab = "reviews")
    {
        var userId = CurrentUserId;
        if (userId is null) return Challenge();

        var vm = await _users.GetProfileAsync(userId.Value, tab);
        if (vm is null) return NotFound();

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var userId = CurrentUserId;
        if (userId is null) return Challenge();

        var profile = await _users.GetProfileAsync(userId.Value, "reviews");
        if (profile is null) return NotFound();

        return View(profile.EditForm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditProfileViewModel model)
    {
        var userId = CurrentUserId;
        if (userId is null) return Challenge();
        if (!ModelState.IsValid) return View(model);

        var result = await _users.UpdateProfileAsync(userId.Value, model);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(result.Field ?? string.Empty, result.Error ?? "Could not save the profile.");
            return View(model);
        }

        // ime/e-mail se nalaze u claimovima pa osvjezava kolacic
        var user = await _users.GetByIdAsync(userId.Value);
        if (user is not null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Name),
                new(ClaimTypes.Email, user.Email),
                new("username", user.Username),
                new("initials", user.Initials()),
                new(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User")
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        }

        Notify("Profile updated.");
        return RedirectToAction(nameof(Index));
    }
}

[Authorize]
public class ReviewsController : BaseController
{
    private readonly IReviewService _reviews;

    public ReviewsController(IReviewService reviews) => _reviews = reviews;

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ReviewFormViewModel form)
    {
        var userId = CurrentUserId;
        if (userId is null) return Challenge();

        if (!ModelState.IsValid)
        {
            NotifyError(string.Join(" ", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)));
            return RedirectToAction("Details", "Movies", new { id = form.MovieId });
        }

        var saved = await _reviews.SaveAsync(userId.Value, form);
        if (!saved) return NotFound();

        Notify("Your review is live.");
        return RedirectToAction("Details", "Movies", new { id = form.MovieId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int movieId)
    {
        var userId = CurrentUserId;
        if (userId is null) return Challenge();

        var deleted = await _reviews.DeleteAsync(id, userId.Value, IsAdminUser);
        if (!deleted) return Forbid();

        Notify("Review deleted.");
        return RedirectToAction("Details", "Movies", new { id = movieId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(CommentFormViewModel form)
    {
        var userId = CurrentUserId;
        if (userId is null) return Challenge();

        if (!ModelState.IsValid)
        {
            NotifyError("Comment cannot be empty.");
            return RedirectBack("Index", "Home");
        }

        var movieId = await _reviews.AddCommentAsync(userId.Value, form);
        if (movieId is null) return NotFound();

        return RedirectToAction("Details", "Movies", new { id = movieId.Value });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(int id, int movieId)
    {
        var userId = CurrentUserId;
        if (userId is null) return Challenge();

        var deleted = await _reviews.DeleteCommentAsync(id, userId.Value, IsAdminUser);
        if (!deleted) return Forbid();

        return RedirectToAction("Details", "Movies", new { id = movieId });
    }
}
