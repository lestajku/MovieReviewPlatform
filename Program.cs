using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MovieReviewPlatform.Data;
using MovieReviewPlatform.Models;
using MovieReviewPlatform.Services;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// Baza podataka
// SQLite je zadano jer radi bez ikakve instalacije. Za SQL Server LocalDB:
//   1) u .csproj odkomentiraj paket Microsoft.EntityFrameworkCore.SqlServer
//   2) ovdje zamijeni UseSqlite sa UseSqlServer
//   3) u appsettings.json koristi "SqlServerConnection"
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Servisi (poslovni sloj)
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IMovieService, MovieService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IFavoriteService, FavoriteService>();
builder.Services.AddScoped<IStatisticsService, StatisticsService>();

// Vanjski APIji
builder.Services.AddHttpClient<ITmdbApiService, TmdbApiService>(client =>
{
    var baseUrl = builder.Configuration["Tmdb:BaseUrl"] ?? "https://api.themoviedb.org/3/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Autentikacija i autorizacija
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.Name = "reel.auth";
        options.Cookie.HttpOnly = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

var app = builder.Build();

// Inicijalizacija baze
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // Za rad s migracijama zamijeni sljedecu liniju sa: db.Database.Migrate();
    db.Database.EnsureCreated();
    DbSeeder.Seed(db, app.Configuration);

    var tmdb = scope.ServiceProvider.GetRequiredService<ITmdbApiService>();
    var importLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await TmdbMovieImporter.ImportPopularAsync(db, tmdb, importLogger);
}

// Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
