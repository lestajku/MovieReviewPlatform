using Microsoft.EntityFrameworkCore;
using MovieReviewPlatform.Data;
using MovieReviewPlatform.Models;
using MovieReviewPlatform.ViewModels;

namespace MovieReviewPlatform.Services;

public interface IReviewService
{
    /// <summary>Kreira novu ili azurira postojecu recenziju korisnika za taj film.</summary>
    Task<bool> SaveAsync(int userId, ReviewFormViewModel form);

    /// <summary>Brise recenziju. Dopusteno autoru ili adminu.</summary>
    Task<bool> DeleteAsync(int reviewId, int actingUserId, bool isAdmin);

    Task<int?> AddCommentAsync(int userId, CommentFormViewModel form);
    Task<bool> DeleteCommentAsync(int commentId, int actingUserId, bool isAdmin);

    Task<List<ReviewViewModel>> GetByUserAsync(int userId);
    Task<List<AdminReviewRowViewModel>> GetAllForAdminAsync();
    Task<int?> GetMovieIdAsync(int reviewId);
}

public class ReviewService : IReviewService
{
    private readonly ApplicationDbContext _db;

    public ReviewService(ApplicationDbContext db) => _db = db;

    public async Task<bool> SaveAsync(int userId, ReviewFormViewModel form)
    {
        var movieExists = await _db.Movies.AnyAsync(m => m.Id == form.MovieId);
        if (!movieExists) return false;

        var review = await _db.Reviews
            .FirstOrDefaultAsync(r => r.MovieId == form.MovieId && r.UserId == userId);

        if (review is null)
        {
            _db.Reviews.Add(new Review
            {
                UserId = userId,
                MovieId = form.MovieId,
                Rating = form.Rating,
                Text = form.Text.Trim(),
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            review.Rating = form.Rating;
            review.Text = form.Text.Trim();
            review.CreatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int reviewId, int actingUserId, bool isAdmin)
    {
        var review = await _db.Reviews
            .Include(r => r.Comments)
            .FirstOrDefaultAsync(r => r.Id == reviewId);

        if (review is null) return false;
        if (review.UserId != actingUserId && !isAdmin) return false;

        _db.Comments.RemoveRange(review.Comments);
        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<int?> AddCommentAsync(int userId, CommentFormViewModel form)
    {
        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == form.ReviewId);
        if (review is null) return null;

        _db.Comments.Add(new Comment
        {
            ReviewId = review.Id,
            UserId = userId,
            Text = form.Text.Trim(),
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return review.MovieId;
    }

    public async Task<bool> DeleteCommentAsync(int commentId, int actingUserId, bool isAdmin)
    {
        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == commentId);
        if (comment is null) return false;
        if (comment.UserId != actingUserId && !isAdmin) return false;

        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<ReviewViewModel>> GetByUserAsync(int userId)
    {
        var reviews = await _db.Reviews
            .Include(r => r.Movie)
            .Include(r => r.User)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return reviews.Select(r => new ReviewViewModel
        {
            Id = r.Id,
            MovieId = r.MovieId,
            MovieTitle = r.Movie?.Title ?? "(deleted)",
            AuthorName = r.User?.Name ?? "Deleted user",
            AuthorInitials = r.User?.Initials() ?? "?",
            Rating = r.Rating,
            Text = r.Text,
            CreatedAt = r.CreatedAt,
            IsMine = true,
            CanModerate = false
        }).ToList();
    }

    public async Task<List<AdminReviewRowViewModel>> GetAllForAdminAsync()
    {
        var reviews = await _db.Reviews
            .Include(r => r.Movie)
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return reviews.Select(r => new AdminReviewRowViewModel
        {
            Id = r.Id,
            MovieId = r.MovieId,
            MovieTitle = r.Movie?.Title ?? "(deleted)",
            Username = r.User?.Username ?? "(deleted)",
            Rating = r.Rating,
            Text = r.Text,
            CreatedAt = r.CreatedAt
        }).ToList();
    }

    public async Task<int?> GetMovieIdAsync(int reviewId) => await _db.Reviews
        .Where(r => r.Id == reviewId)
        .Select(r => (int?)r.MovieId)
        .FirstOrDefaultAsync();
}
