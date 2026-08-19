using System.ComponentModel.DataAnnotations;

namespace MovieReviewPlatform.ViewModels;

/// <summary>Podaci za jednu karticu filma (_MovieCard.cshtml).</summary>
public class MovieCardViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Year { get; set; }
    public string GenresText { get; set; } = string.Empty;
    public string? PosterUrl { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public bool IsFavorite { get; set; }

    public string RatingText => AverageRating > 0 ? AverageRating.ToString("0.0") : "—";
}

/// <summary>Filteri kataloga, vezu se iz query stringa.</summary>
public class MovieFilterViewModel
{
    public string? Q { get; set; }
    public string? Genre { get; set; }
    public string? YearRange { get; set; }
    public int MinRating { get; set; }
    public string Sort { get; set; } = "popular";
}

public class CatalogViewModel
{
    public MovieFilterViewModel Filter { get; set; } = new();
    public List<MovieCardViewModel> Movies { get; set; } = new();
    public List<string> Genres { get; set; } = new();

    public string CountText => Movies.Count == 1 ? "1 movie found" : $"{Movies.Count} movies found";
}

public class HomeViewModel
{
    public List<MovieCardViewModel> Trending { get; set; } = new();
    public List<MovieCardViewModel> Newest { get; set; } = new();
    public List<MovieCardViewModel> TopRated { get; set; } = new();
    public List<MovieCardViewModel> Recommended { get; set; } = new();

    public bool ShowRecommended => Recommended.Count > 0;
}

public class CommentViewModel
{
    public int Id { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool CanDelete { get; set; }
}

public class ReviewViewModel
{
    public int Id { get; set; }
    public int MovieId { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorInitials { get; set; } = "?";
    public int Rating { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsMine { get; set; }
    public bool CanModerate { get; set; }
    public List<CommentViewModel> Comments { get; set; } = new();

    public string RatingText => $"{Rating}/10";
}

public class MovieDetailsViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Duration { get; set; }
    public string Director { get; set; } = string.Empty;
    public string Cast { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? PosterUrl { get; set; }
    public List<string> Genres { get; set; } = new();

    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public bool IsFavorite { get; set; }

    public List<ReviewViewModel> Reviews { get; set; } = new();
    public List<MovieCardViewModel> Similar { get; set; } = new();

    /// <summary>Recenzija prijavljenog korisnika (ako postoji), puni formu.</summary>
    public ReviewFormViewModel ReviewForm { get; set; } = new();
    public bool HasOwnReview { get; set; }

    public string RatingText => AverageRating > 0 ? AverageRating.ToString("0.0") : "—";
    public string ReviewCountText => ReviewCount == 1 ? "1 review" : $"{ReviewCount} reviews";
}

public class ReviewFormViewModel
{
    public int MovieId { get; set; }

    [Range(1, 10, ErrorMessage = "Pick a rating from 1 to 10.")]
    public int Rating { get; set; }

    [Required(ErrorMessage = "Write a few words about the film.")]
    [StringLength(2000, MinimumLength = 3, ErrorMessage = "Reviews are between 3 and 2000 characters.")]
    public string Text { get; set; } = string.Empty;
}

public class CommentFormViewModel
{
    public int ReviewId { get; set; }

    [Required(ErrorMessage = "Comment cannot be empty.")]
    [StringLength(1000, MinimumLength = 1)]
    public string Text { get; set; } = string.Empty;
}
