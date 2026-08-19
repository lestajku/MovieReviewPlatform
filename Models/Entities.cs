using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MovieReviewPlatform.Models;

public enum UserRole
{
    User = 0,
    Admin = 1
}

public class User
{
    public int Id { get; set; }

    [Required, StringLength(30, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Opcionalno ime i prezime; kad su popunjeni, Name se izvodi iz njih (inace ostaje Username).</summary>
    [StringLength(50)]
    public string? FirstName { get; set; }

    [StringLength(50)]
    public string? LastName { get; set; }

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.User;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    [StringLength(400)]
    public string? Bio { get; set; }

    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

    [NotMapped]
    public bool IsAdmin => Role == UserRole.Admin;

    /// <summary>Inicijali za avatar (npr. "Marcus Hale" postaje "MH").</summary>
    public string Initials()
    {
        var parts = (Name ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(p => char.ToUpperInvariant(p[0]));
        var result = string.Concat(parts);
        return string.IsNullOrEmpty(result) ? "?" : result;
    }
}

public class Movie
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Range(1888, 2100)]
    public int Year { get; set; }

    [Range(1, 600)]
    public int Duration { get; set; }

    [Required, StringLength(120)]
    public string Director { get; set; } = string.Empty;

    [StringLength(500)]
    public string Cast { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [StringLength(500)]
    public string? PosterUrl { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public int Views { get; set; }

    /// <summary>Rating s TMDBa, koristi se dok film nema nijednu recenziju u appu.</summary>
    public double? ExternalRating { get; set; }

    public ICollection<MovieGenre> MovieGenres { get; set; } = new List<MovieGenre>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

    [NotMapped]
    public IEnumerable<string> GenreNames => MovieGenres
        .Select(mg => mg.Genre?.Name ?? string.Empty)
        .Where(n => n.Length > 0)
        .OrderBy(n => n);

    [NotMapped]
    public string GenresText => string.Join(", ", GenreNames);
}

public class Genre
{
    public int Id { get; set; }

    [Required, StringLength(50)]
    public string Name { get; set; } = string.Empty;

    public ICollection<MovieGenre> MovieGenres { get; set; } = new List<MovieGenre>();
}

/// <summary>Vezna tablica za M:N vezu film i zanr.</summary>
public class MovieGenre
{
    public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;

    public int GenreId { get; set; }
    public Genre Genre { get; set; } = null!;
}

public class Review
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;

    [Range(1, 10)]
    public int Rating { get; set; }

    [Required, StringLength(2000, MinimumLength = 3)]
    public string Text { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
}

public class Comment
{
    public int Id { get; set; }

    public int ReviewId { get; set; }
    public Review Review { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    [Required, StringLength(1000, MinimumLength = 1)]
    public string Text { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Favorite
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
