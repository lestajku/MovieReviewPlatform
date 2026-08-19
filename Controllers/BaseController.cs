using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace MovieReviewPlatform.Controllers;


/// Zajednicka logika za citanje podataka o prijavljenom korisniku iz claimova.

public abstract class BaseController : Controller
{
    protected int? CurrentUserId
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    protected bool IsLoggedIn => CurrentUserId is not null;

    protected bool IsAdminUser => User.IsInRole("Admin");

    /// Poruka koja se jednokratno prikazuje nakon redirecta.
    protected void Notify(string message) => TempData["Notice"] = message;

    protected void NotifyError(string message) => TempData["Error"] = message;

    /// Vraca korisnika na stranicu s koje je dosao, uz sigurnosnu provjeru.
    protected IActionResult RedirectBack(string fallbackAction = "Index", string? fallbackController = null)
    {
        var referer = Request.Headers["Referer"].ToString();
        if (!string.IsNullOrEmpty(referer)
            && Uri.TryCreate(referer, UriKind.Absolute, out var uri)
            && string.Equals(uri.Authority, Request.Host.Value, StringComparison.OrdinalIgnoreCase))
        {
            return Redirect(uri.PathAndQuery);
        }

        return fallbackController is null
            ? RedirectToAction(fallbackAction)
            : RedirectToAction(fallbackAction, fallbackController);
    }
}
