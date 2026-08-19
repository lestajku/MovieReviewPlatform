using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MovieReviewPlatform.Data;
using MovieReviewPlatform.Models;
using MovieReviewPlatform.ViewModels;

namespace MovieReviewPlatform.Services;

/// <summary>Rezultat operacije s porukom o gresci koja se moze prikazati u formi.</summary>
public record OperationResult(bool Succeeded, string? Error = null, string? Field = null)
{
    public static OperationResult Ok() => new(true);
    public static OperationResult Fail(string error, string? field = null) => new(false, error, field);
}

public interface IUserService
{
    Task<User?> ValidateCredentialsAsync(string username, string password);
    Task<(OperationResult Result, User? User)> RegisterAsync(RegisterViewModel model);
    Task<User?> GetByIdAsync(int id);

    Task<ProfileViewModel?> GetProfileAsync(int userId, string tab);
    Task<OperationResult> UpdateProfileAsync(int userId, EditProfileViewModel model);

    Task<List<AdminUserRowViewModel>> GetAdminRowsAsync(int currentUserId);
    Task<OperationResult> ToggleRoleAsync(int userId);
    Task<OperationResult> DeleteAsync(int userId, int currentUserId);
}

public class UserService : IUserService
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<User> _hasher;

    public UserService(ApplicationDbContext db, IPasswordHasher<User> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    // autentikacija

    public async Task<User?> ValidateCredentialsAsync(string username, string password)
    {
        var normalized = (username ?? string.Empty).Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == normalized);
        if (user is null) return null;

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed) return null;

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, password);
            await _db.SaveChangesAsync();
        }

        return user;
    }

    /// <summary>Izvodi prikazno ime iz opcionalnog imena/prezimena, inace pada natrag na username.</summary>
    private static string ResolveDisplayName(string? firstName, string? lastName, string username)
    {
        var combined = $"{firstName} {lastName}".Trim();
        while (combined.Contains("  ")) combined = combined.Replace("  ", " ");
        return combined.Length > 0 ? combined : username;
    }

    public async Task<(OperationResult Result, User? User)> RegisterAsync(RegisterViewModel model)
    {
        var username = model.Username.Trim();
        var email = model.Email.Trim();
        var firstName = string.IsNullOrWhiteSpace(model.FirstName) ? null : model.FirstName.Trim();
        var lastName = string.IsNullOrWhiteSpace(model.LastName) ? null : model.LastName.Trim();

        if (await _db.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower()))
            return (OperationResult.Fail("That username is taken.", nameof(model.Username)), null);

        if (await _db.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower()))
            return (OperationResult.Fail("An account with that email already exists.", nameof(model.Email)), null);

        var user = new User
        {
            Username = username,
            Name = ResolveDisplayName(firstName, lastName, username),
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Role = UserRole.User,
            JoinedAt = DateTime.UtcNow
        };
        user.PasswordHash = _hasher.HashPassword(user, model.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return (OperationResult.Ok(), user);
    }

    public async Task<User?> GetByIdAsync(int id) => await _db.Users.FindAsync(id);

    // profil

    public async Task<ProfileViewModel?> GetProfileAsync(int userId, string tab)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return null;

        var reviews = await _db.Reviews
            .Include(r => r.Movie)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var ratedMovies = await _db.Movies
            .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
            .Include(m => m.Reviews)
            .Where(m => m.Reviews.Any(r => r.UserId == userId))
            .ToListAsync();

        var favorites = await _db.Movies
            .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
            .Include(m => m.Reviews)
            .Where(m => m.Favorites.Any(f => f.UserId == userId))
            .ToListAsync();

        var favIds = favorites.Select(m => m.Id).ToHashSet();

        static MovieCardViewModel ToCard(Movie m, HashSet<int> favIds) => new()
        {
            Id = m.Id,
            Title = m.Title,
            Year = m.Year,
            GenresText = m.GenresText,
            PosterUrl = m.PosterUrl,
            AverageRating = m.Reviews.Count == 0 ? 0 : m.Reviews.Average(r => r.Rating),
            ReviewCount = m.Reviews.Count,
            IsFavorite = favIds.Contains(m.Id)
        };

        return new ProfileViewModel
        {
            Name = user.Name,
            Username = user.Username,
            Email = user.Email,
            Initials = user.Initials(),
            RoleLabel = user.IsAdmin ? "Admin" : "User",
            JoinedAt = user.JoinedAt,
            Bio = user.Bio,
            Tab = tab,
            AverageRating = reviews.Count == 0 ? 0 : reviews.Average(r => r.Rating),
            Reviews = reviews.Select(r => new ReviewViewModel
            {
                Id = r.Id,
                MovieId = r.MovieId,
                MovieTitle = r.Movie?.Title ?? "(deleted)",
                AuthorName = user.Name,
                AuthorInitials = user.Initials(),
                Rating = r.Rating,
                Text = r.Text,
                CreatedAt = r.CreatedAt,
                IsMine = true
            }).ToList(),
            RatedMovies = ratedMovies.Select(m => ToCard(m, favIds)).ToList(),
            Favorites = favorites.Select(m => ToCard(m, favIds)).ToList(),
            EditForm = new EditProfileViewModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Bio = user.Bio
            }
        };
    }

    public async Task<OperationResult> UpdateProfileAsync(int userId, EditProfileViewModel model)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return OperationResult.Fail("User not found.");

        var email = model.Email.Trim();
        if (await _db.Users.AnyAsync(u => u.Id != userId && u.Email.ToLower() == email.ToLower()))
            return OperationResult.Fail("An account with that email already exists.", nameof(model.Email));

        var firstName = string.IsNullOrWhiteSpace(model.FirstName) ? null : model.FirstName.Trim();
        var lastName = string.IsNullOrWhiteSpace(model.LastName) ? null : model.LastName.Trim();

        user.FirstName = firstName;
        user.LastName = lastName;
        user.Name = ResolveDisplayName(firstName, lastName, user.Username);
        user.Email = email;
        user.Bio = string.IsNullOrWhiteSpace(model.Bio) ? null : model.Bio.Trim();

        await _db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    // administracija

    public async Task<List<AdminUserRowViewModel>> GetAdminRowsAsync(int currentUserId)
    {
        var users = await _db.Users
            .Include(u => u.Reviews)
            .OrderBy(u => u.Name)
            .ToListAsync();

        var adminCount = users.Count(u => u.Role == UserRole.Admin);

        return users.Select(u =>
        {
            var isLastAdmin = u.Role == UserRole.Admin && adminCount <= 1;
            return new AdminUserRowViewModel
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                IsAdmin = u.Role == UserRole.Admin,
                ReviewCount = u.Reviews.Count,
                JoinedAt = u.JoinedAt,
                RoleToggleDisabled = isLastAdmin,
                DeleteDisabled = isLastAdmin || u.Id == currentUserId
            };
        }).ToList();
    }

    public async Task<OperationResult> ToggleRoleAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return OperationResult.Fail("User not found.");

        if (user.Role == UserRole.Admin)
        {
            var adminCount = await _db.Users.CountAsync(u => u.Role == UserRole.Admin);
            if (adminCount <= 1)
                return OperationResult.Fail("The last admin cannot be demoted.");
            user.Role = UserRole.User;
        }
        else
        {
            user.Role = UserRole.Admin;
        }

        await _db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    public async Task<OperationResult> DeleteAsync(int userId, int currentUserId)
    {
        if (userId == currentUserId)
            return OperationResult.Fail("You cannot delete your own account here.");

        var user = await _db.Users
            .Include(u => u.Reviews)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null) return OperationResult.Fail("User not found.");

        if (user.Role == UserRole.Admin && await _db.Users.CountAsync(u => u.Role == UserRole.Admin) <= 1)
            return OperationResult.Fail("The last admin cannot be deleted.");

        // komentari korisnika + komentari na njegovim recenzijama (FK je Restrict, brisemo rucno)
        var reviewIds = user.Reviews.Select(r => r.Id).ToList();
        var comments = await _db.Comments
            .Where(c => c.UserId == userId || reviewIds.Contains(c.ReviewId))
            .ToListAsync();
        _db.Comments.RemoveRange(comments);

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return OperationResult.Ok();
    }
}
