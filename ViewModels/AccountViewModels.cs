using System.ComponentModel.DataAnnotations;

namespace MovieReviewPlatform.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Enter your username.")]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; } = true;

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(30, MinimumLength = 3, ErrorMessage = "Username is between 3 and 30 characters.")]
    [RegularExpression("^[a-zA-Z0-9_.-]+$", ErrorMessage = "Use letters, numbers, dot, dash or underscore.")]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [StringLength(50)]
    [Display(Name = "First name (optional)")]
    public string? FirstName { get; set; }

    [StringLength(50)]
    [Display(Name = "Last name (optional)")]
    public string? LastName { get; set; }

    [Required(ErrorMessage = "Enter a valid email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [RegularExpression(AccountValidation.EmailPattern, ErrorMessage = AccountValidation.EmailErrorMessage)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 7, ErrorMessage = "Use at least 7 characters.")]
    [RegularExpression(AccountValidation.PasswordPattern, ErrorMessage = AccountValidation.PasswordErrorMessage)]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>Zajednicka pravila validacije za lozinku i email (registracija + uredivanje profila).</summary>
public static class AccountValidation
{
    // min 7 znakova, barem jedno veliko slovo, jedan broj i jedan poseban znak
    public const string PasswordPattern = @"^(?=.*[A-Z])(?=.*[0-9])(?=.*[^A-Za-z0-9]).{7,}$";
    public const string PasswordErrorMessage = "Password must be at least 7 characters and include an uppercase letter, a number, and a special character.";

    // odbija adrese poput "e@g.c", domena mora imati tocku i TLD od barem 2 slova
    public const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[A-Za-z]{2,}$";
    public const string EmailErrorMessage = "Enter a real email address (e.g. name@example.com).";
}

public class EditProfileViewModel
{
    [StringLength(50)]
    [Display(Name = "First name (optional)")]
    public string? FirstName { get; set; }

    [StringLength(50)]
    [Display(Name = "Last name (optional)")]
    public string? LastName { get; set; }

    [Required(ErrorMessage = "Enter a valid email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [RegularExpression(AccountValidation.EmailPattern, ErrorMessage = AccountValidation.EmailErrorMessage)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [StringLength(400, ErrorMessage = "Bio is limited to 400 characters.")]
    [Display(Name = "Bio")]
    public string? Bio { get; set; }
}

public class ProfileViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Initials { get; set; } = "?";
    public string RoleLabel { get; set; } = "User";
    public DateTime JoinedAt { get; set; }
    public string? Bio { get; set; }

    /// <summary>reviews | rated | favorites</summary>
    public string Tab { get; set; } = "reviews";

    public List<ReviewViewModel> Reviews { get; set; } = new();
    public List<MovieCardViewModel> RatedMovies { get; set; } = new();
    public List<MovieCardViewModel> Favorites { get; set; } = new();

    public double AverageRating { get; set; }
    public string AverageRatingText => Reviews.Count == 0 ? "—" : Math.Round(AverageRating, 1).ToString("0.0");

    public EditProfileViewModel EditForm { get; set; } = new();
    public bool EditOpen { get; set; }
}

public class RankingRowViewModel
{
    public int Rank { get; set; }
    public int MovieId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string MetaText { get; set; } = string.Empty;
    public string ValueText { get; set; } = string.Empty;

    public string RankText => Rank.ToString("00");
}

public class TopReviewerViewModel
{
    public int Rank { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Initials { get; set; } = "?";
    public int ReviewCount { get; set; }

    public string RankText => Rank.ToString("00");
    public string CountText => ReviewCount == 1 ? "1 review" : $"{ReviewCount} reviews";
}

public class RankingsViewModel
{
    public List<RankingRowViewModel> BestRated { get; set; } = new();
    public List<RankingRowViewModel> MostPopular { get; set; } = new();
    public List<TopReviewerViewModel> TopReviewers { get; set; } = new();
}
