using Microsoft.EntityFrameworkCore;
using MovieReviewPlatform.Models;

namespace MovieReviewPlatform.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<MovieGenre> MovieGenres => Set<MovieGenre>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Favorite> Favorites => Set<Favorite>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // User
        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Role).HasConversion<int>();
        });

        // Genre
        b.Entity<Genre>()
            .HasIndex(g => g.Name)
            .IsUnique();

        // Movie
        b.Entity<Movie>()
            .HasIndex(m => m.Title);

        // MovieGenre (M:N)
        b.Entity<MovieGenre>(e =>
        {
            e.HasKey(mg => new { mg.MovieId, mg.GenreId });

            e.HasOne(mg => mg.Movie)
                .WithMany(m => m.MovieGenres)
                .HasForeignKey(mg => mg.MovieId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(mg => mg.Genre)
                .WithMany(g => g.MovieGenres)
                .HasForeignKey(mg => mg.GenreId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Review
        b.Entity<Review>(e =>
        {
            // jedan korisnik ima najvise jedna recenzija po filmu
            e.HasIndex(r => new { r.UserId, r.MovieId }).IsUnique();

            e.HasOne(r => r.Movie)
                .WithMany(m => m.Reviews)
                .HasForeignKey(r => r.MovieId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(r => r.User)
                .WithMany(u => u.Reviews)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Comment
        b.Entity<Comment>(e =>
        {
            e.HasOne(c => c.Review)
                .WithMany(r => r.Comments)
                .HasForeignKey(c => c.ReviewId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict jer bi inace nastao dvostruki cascade put (User prema Review prema Comment
            // i User prema Comment), sto SQL Server ne dopusta. Komentari se rucno brisu u UserService.
            e.HasOne(c => c.User)
                .WithMany(u => u.Comments)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Favorite
        b.Entity<Favorite>(e =>
        {
            e.HasIndex(f => new { f.UserId, f.MovieId }).IsUnique();

            e.HasOne(f => f.User)
                .WithMany(u => u.Favorites)
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(f => f.Movie)
                .WithMany(m => m.Favorites)
                .HasForeignKey(f => f.MovieId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
