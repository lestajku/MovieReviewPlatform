using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MovieReviewPlatform.ViewModels;

/// <summary>Prihvaca samo slikovne datoteke razumne velicine; prazno polje (bez uploada) uvijek prolazi.</summary>
public class AllowedImageFileAttribute : ValidationAttribute
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    private const long MaxSizeBytes = 5 * 1024 * 1024;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not IFormFile file || file.Length == 0) return ValidationResult.Success;

        if (file.Length > MaxSizeBytes)
            return new ValidationResult("Image must be 5 MB or smaller.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return new ValidationResult("Upload a JPG, PNG, WEBP or GIF image.");

        return ValidationResult.Success;
    }
}

public class AdminMovieRowViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Year { get; set; }
    public string GenresText { get; set; } = string.Empty;
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }

    public string RatingText => AverageRating > 0 ? AverageRating.ToString("0.0") : "—";
}

public class AdminUserRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public int ReviewCount { get; set; }
    public DateTime JoinedAt { get; set; }

    /// <summary>Zadnji admin ili trenutno prijavljeni korisnik se ne smiju degradirati/obrisati.</summary>
    public bool RoleToggleDisabled { get; set; }
    public bool DeleteDisabled { get; set; }

    public string RoleLabel => IsAdmin ? "Admin" : "User";
    public string RoleTagClass => IsAdmin ? "tag tag-accent" : "tag tag-neutral";
    public string ToggleRoleLabel => IsAdmin ? "Make user" : "Make admin";
}

public class AdminReviewRowViewModel
{
    public int Id { get; set; }
    public int MovieId { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public string RatingText => $"{Rating}/10";
    public string Snippet => Text.Length > 90 ? Text[..90] + "…" : Text;
}

public class AdminStatsViewModel
{
    public int TotalUsers { get; set; }
    public int TotalMovies { get; set; }
    public int TotalReviews { get; set; }
    public int TotalComments { get; set; }
    public double AverageRating { get; set; }

    public string AverageRatingText => AverageRating > 0 ? AverageRating.ToString("0.0") : "—";
}

public class AdminDashboardViewModel
{
    /// <summary>movies | users | reviews | stats</summary>
    public string Tab { get; set; } = "movies";

    public List<AdminMovieRowViewModel> Movies { get; set; } = new();
    public List<AdminUserRowViewModel> Users { get; set; } = new();
    public List<AdminReviewRowViewModel> Reviews { get; set; } = new();
    public AdminStatsViewModel Stats { get; set; } = new();
}

public class MovieFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200)]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [Range(1888, 2100, ErrorMessage = "Enter a year between 1888 and 2100.")]
    [Display(Name = "Year")]
    public int Year { get; set; } = DateTime.UtcNow.Year;

    [Range(1, 600, ErrorMessage = "Duration must be between 1 and 600 minutes.")]
    [Display(Name = "Duration (min)")]
    public int Duration { get; set; }

    [Required(ErrorMessage = "Add at least one genre.")]
    [StringLength(200)]
    [Display(Name = "Genres (comma-separated)")]
    public string Genres { get; set; } = string.Empty;

    [Required(ErrorMessage = "Director is required.")]
    [StringLength(120)]
    [Display(Name = "Director")]
    public string Director { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Cast (comma-separated)")]
    public string Cast { get; set; } = string.Empty;

    [StringLength(2000)]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [AllowedImageFile]
    [Display(Name = "Upload poster image (optional)")]
    public IFormFile? PosterFile { get; set; }

    [Url(ErrorMessage = "Enter a valid image URL.")]
    [StringLength(500)]
    [Display(Name = "…or poster URL")]
    public string? PosterUrl { get; set; }

    /// <summary>Postavlja se u viewu kad se uredjuje film, da se moze prikazati trenutni poster.</summary>
    public string? CurrentPosterUrl { get; set; }

    public bool IsEdit => Id > 0;
    public string Heading => IsEdit ? "Edit movie" : "Add movie";
    public string SaveLabel => IsEdit ? "Save changes" : "Add movie";
}
