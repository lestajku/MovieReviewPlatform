using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using MovieReviewPlatform.Models;
using MovieReviewPlatform.Services;
using MovieReviewPlatform.ViewModels;

namespace MovieReviewPlatform.Controllers;

public class AccountController : BaseController
{
    private readonly IUserService _users;

    public AccountController(IUserService users) => _users = users;

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (IsLoggedIn) return RedirectToAction("Index", "Home");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _users.ValidateCredentialsAsync(model.Username, model.Password);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Wrong username or password. Try a demo account below, or sign up.");
            return View(model);
        }

        await SignInAsync(user, model.RememberMe);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (IsLoggedIn) return RedirectToAction("Index", "Home");
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var (result, user) = await _users.RegisterAsync(model);
        if (!result.Succeeded || user is null)
        {
            ModelState.AddModelError(result.Field ?? string.Empty, result.Error ?? "Registration failed.");
            return View(model);
        }

        await SignInAsync(user, rememberMe: true);
        Notify($"Welcome to REEL, {user.Name}.");
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    /// Kreira claimove i postavlja autentikacijski kolacic.
    private async Task SignInAsync(User user, bool rememberMe)
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

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            });
    }
}
