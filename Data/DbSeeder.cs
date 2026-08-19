using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MovieReviewPlatform.Models;

namespace MovieReviewPlatform.Data;

/// <summary>
/// Puni bazu pocetnim podacima. Pokrece se samo ako je tablica Users prazna.
/// Lozinke se ne drze u kodu vec dolaze iz konfiguracije (user secrets):
/// Seed:AdminPassword i Seed:UserPassword.
/// </summary>
public static class DbSeeder
{
    private const string PosterBase = "https://image.tmdb.org/t/p/w500";

    public static void Seed(ApplicationDbContext db, IConfiguration config)
    {
        EnsureSchemaUpgrades(db);

        if (db.Users.Any())
        {
            BackfillPosters(db);
            return;
        }

        SeedFull(db, config);
        BackfillPosters(db);
    }

    /// <summary>
    /// EnsureCreated() ne mijenja shemu postojece baze, pa nove stupce dodane u model
    /// (npr. Movie.ExternalRating) treba rucno dodati na vec postojecim bazama. Sigurno je
    /// pozivati vise puta, ako stupac vec postoji, greska se samo ignorira.
    /// </summary>
    private static void EnsureSchemaUpgrades(ApplicationDbContext db)
    {
        TryAddColumn(db, "ALTER TABLE Movies ADD COLUMN ExternalRating REAL NULL");
        TryAddColumn(db, "ALTER TABLE Users ADD COLUMN FirstName TEXT NULL");
        TryAddColumn(db, "ALTER TABLE Users ADD COLUMN LastName TEXT NULL");
    }

    private static void TryAddColumn(ApplicationDbContext db, string alterSql)
    {
        try
        {
            db.Database.ExecuteSqlRaw(alterSql);
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
        {
        }
    }

    private static void SeedFull(ApplicationDbContext db, IConfiguration config)
    {
        var adminPassword = config["Seed:AdminPassword"]
            ?? throw new InvalidOperationException("Seed:AdminPassword nije postavljen. Pokreni: dotnet user-secrets set \"Seed:AdminPassword\" \"...\"");
        var userPassword = config["Seed:UserPassword"]
            ?? throw new InvalidOperationException("Seed:UserPassword nije postavljen. Pokreni: dotnet user-secrets set \"Seed:UserPassword\" \"...\"");

        var hasher = new PasswordHasher<User>();
        static DateTime D(string iso) => DateTime.SpecifyKind(DateTime.Parse(iso), DateTimeKind.Utc);

        // KORISNICI
        var userData = new (string Key, string Username, string Name, string Email, UserRole Role, string Joined, string Bio)[]
        {
            ("u1", "admin",    "Site Admin",  "admin@reel.app",         UserRole.Admin, "2025-11-01", "Keeps the lights on."),
            ("u2", "marcus_h", "Marcus Hale", "marcus.hale@mail.com",   UserRole.User,  "2025-12-04", "Sci-fi and crime dramas, mostly."),
            ("u3", "ana_r",    "Ana Ricci",   "ana.ricci@mail.com",     UserRole.User,  "2026-01-14", "Here for the animation and the Wes Anderson symmetry."),
            ("u4", "ivan_b",   "Ivan Bell",   "ivan.bell@mail.com",     UserRole.User,  "2026-01-29", ""),
            ("u5", "petra_n",  "Petra Novak", "petra.novak@mail.com",   UserRole.User,  "2026-02-20", "Reviewing everything with a soundtrack worth remembering."),
            ("u6", "luke_b",   "Luke Bennett","luke.bennett@mail.com",  UserRole.User,  "2026-03-11", ""),
            ("u7", "emma_c",   "Emma Cole",   "emma.cole@mail.com",     UserRole.User,  "2026-04-30", "Slow cinema apologist.")
        };

        var users = new Dictionary<string, User>();
        foreach (var u in userData)
        {
            var user = new User
            {
                Username = u.Username,
                Name = u.Name,
                Email = u.Email,
                Role = u.Role,
                JoinedAt = D(u.Joined),
                Bio = string.IsNullOrWhiteSpace(u.Bio) ? null : u.Bio
            };
            user.PasswordHash = hasher.HashPassword(user, u.Role == UserRole.Admin ? adminPassword : userPassword);
            users[u.Key] = user;
            db.Users.Add(user);
        }

        // FILMOVI
        var movieData = new (string Key, string Title, int Year, string Genres, int Duration, string Director, string Cast, string Description, string Added, int Views)[]
        {
            ("m1","The Godfather",1972,"Crime,Drama",175,"Francis Ford Coppola","Marlon Brando, Al Pacino, James Caan","The aging patriarch of a New York crime family transfers control of his empire to his reluctant youngest son.","2026-03-02",18400),
            ("m2","Pulp Fiction",1994,"Crime",154,"Quentin Tarantino","John Travolta, Samuel L. Jackson, Uma Thurman","The lives of two hitmen, a boxer, and a pair of small-time crooks intertwine across Los Angeles.","2026-03-10",21200),
            ("m3","The Matrix",1999,"Sci-Fi,Action",136,"Lana & Lilly Wachowski","Keanu Reeves, Laurence Fishburne, Carrie-Anne Moss","A computer programmer discovers the world he knows is a simulation and joins a rebellion against its machine rulers.","2026-04-01",26800),
            ("m4","Parasite",2019,"Thriller,Drama",132,"Bong Joon-ho","Song Kang-ho, Lee Sun-kyun, Cho Yeo-jeong","A poor family schemes its way into the household of a wealthy one, with consequences neither expects.","2026-07-18",15600),
            ("m5","Spirited Away",2001,"Animation",125,"Hayao Miyazaki","Rumi Hiiragi, Miyu Irino","A young girl trapped in a spirit world must find a way to free her parents and return home.","2026-05-22",14100),
            ("m6","Inception",2010,"Sci-Fi,Thriller",148,"Christopher Nolan","Leonardo DiCaprio, Joseph Gordon-Levitt, Elliot Page","A team of dream thieves is hired to plant an idea in the mind of a seemingly impossible target.","2026-03-28",24300),
            ("m7","The Grand Budapest Hotel",2014,"Comedy",99,"Wes Anderson","Ralph Fiennes, Tony Revolori, F. Murray Abraham","A legendary concierge and his loyal lobby boy get caught up in a stolen painting and a family fortune.","2026-06-14",9200),
            ("m8","Get Out",2017,"Horror,Thriller",104,"Jordan Peele","Daniel Kaluuya, Allison Williams, Bradley Whitford","A young man uncovers a disturbing secret while visiting his girlfriend's family estate.","2026-08-02",12700),
            ("m9","La La Land",2016,"Romance",128,"Damien Chazelle","Ryan Gosling, Emma Stone","An actress and a jazz musician try to build their careers in Los Angeles as their relationship evolves.","2026-04-19",16800),
            ("m10","Mad Max: Fury Road",2015,"Action",120,"George Miller","Tom Hardy, Charlize Theron","A desert chase turns into a war against a tyrant and his army.","2026-05-05",19500),
            ("m11","The Shawshank Redemption",1994,"Drama",142,"Frank Darabont","Tim Robbins, Morgan Freeman","A banker convicted of a murder he didn't commit builds friendship and hope over decades in prison.","2026-03-15",28100),
            ("m12","Amelie",2001,"Romance,Comedy",122,"Jean-Pierre Jeunet","Audrey Tautou, Mathieu Kassovitz","A dreamy Parisian waitress quietly reshapes the lives of people around her.","2026-07-01",8600),
            ("m13","No Country for Old Men",2007,"Crime,Thriller",122,"Ethan & Joel Coen","Tommy Lee Jones, Javier Bardem, Josh Brolin","Money found at a crime scene draws a relentless killer and a weary sheriff into pursuit.","2026-06-27",11300),
            ("m14","Interstellar",2014,"Sci-Fi,Drama",169,"Christopher Nolan","Matthew McConaughey, Anne Hathaway","A group of explorers travel through a wormhole in search of a new home for humanity.","2026-04-08",25700),
            ("m15","Whiplash",2014,"Drama",106,"Damien Chazelle","Miles Teller, J.K. Simmons","A young drummer at an elite music school clashes with his ruthless instructor.","2026-08-10",10400),
            ("m16","Knives Out",2019,"Mystery,Comedy",130,"Rian Johnson","Daniel Craig, Ana de Armas, Chris Evans","A private detective investigates an eccentric family after the death of a famous crime novelist.","2026-07-25",13900),
            ("m17","Everything Everywhere All at Once",2022,"Sci-Fi,Comedy",140,"Daniel Kwan & Daniel Scheinert","Michelle Yeoh, Ke Huy Quan, Jamie Lee Curtis","A laundromat owner discovers she must connect with versions of herself across parallel universes.","2026-08-14",17200),
            ("m18","Oppenheimer",2023,"Drama,Biography",180,"Christopher Nolan","Cillian Murphy, Emily Blunt, Robert Downey Jr.","The story of physicist J. Robert Oppenheimer and the development of the atomic bomb during World War II.","2026-08-16",23600)
        };

        var genres = new Dictionary<string, Genre>(StringComparer.OrdinalIgnoreCase);
        var movies = new Dictionary<string, Movie>();

        foreach (var m in movieData)
        {
            var movie = new Movie
            {
                Title = m.Title,
                Year = m.Year,
                Duration = m.Duration,
                Director = m.Director,
                Cast = m.Cast,
                Description = m.Description,
                AddedAt = D(m.Added),
                Views = m.Views
            };

            foreach (var name in m.Genres.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!genres.TryGetValue(name, out var genre))
                {
                    genre = new Genre { Name = name };
                    genres[name] = genre;
                    db.Genres.Add(genre);
                }
                movie.MovieGenres.Add(new MovieGenre { Movie = movie, Genre = genre });
            }

            movies[m.Key] = movie;
            db.Movies.Add(movie);
        }

        // RECENZIJE
        var reviewData = new (string Key, string User, string Movie, int Rating, string Text, string Date)[]
        {
            ("r1","u2","m1",10,"A masterclass in pacing - every scene earns its runtime. The dinner table alone is a film school.","2026-05-02"),
            ("r2","u3","m1",9,"Brando's performance still holds up. Long, but never boring.","2026-05-20"),
            ("r3","u5","m1",9,"The score does half the storytelling. One of the greats.","2026-06-01"),
            ("r4","u7","m1",8,"Brilliant, if a little slow in the middle act for a modern viewer.","2026-07-10"),
            ("r5","u2","m2",9,"The nonlinear structure still feels fresh. Dialogue is endlessly quotable.","2026-04-15"),
            ("r6","u4","m2",8,"Style over substance in places, but the style is that good.","2026-05-08"),
            ("r7","u6","m2",9,"Travolta and Jackson are perfect together. Rewatched it three times this year.","2026-06-19"),
            ("r8","u3","m2",7,"Enjoyed it, though the shock value has aged unevenly.","2026-07-22"),
            ("r9","u2","m3",10,"Redefined action cinema. The bullet-time sequences are still copied everywhere.","2026-04-05"),
            ("r10","u4","m3",9,"Philosophy wrapped in leather trench coats. Somehow it works.","2026-04-22"),
            ("r11","u5","m3",8,"Great ideas, and the fight choreography holds up better than most 90s effects.","2026-05-30"),
            ("r12","u7","m3",9,"Still the best 'what is reality' movie for my money.","2026-06-11"),
            ("r13","u1","m3",8,"A genre landmark that's aged remarkably well.","2026-08-05"),
            ("r14","u3","m4",10,"Every genre shift lands. Rewatch it and you'll catch three new details.","2026-07-20"),
            ("r15","u6","m4",10,"The staircase sequence is one of the best in modern film.","2026-07-25"),
            ("r16","u2","m4",9,"Tense, funny, and devastating in equal measure.","2026-08-03"),
            ("r17","u3","m5",10,"Miyazaki's imagination has no ceiling. The bathhouse alone is worth the watch.","2026-05-25"),
            ("r18","u7","m5",9,"Gorgeous animation, gentle pacing. A comfort watch.","2026-06-14"),
            ("r19","u2","m6",9,"The layered dream structure rewards a second viewing.","2026-04-02"),
            ("r20","u4","m6",8,"The exposition is heavy but the third act payoff is worth it.","2026-04-30"),
            ("r21","u5","m6",9,"Hans Zimmer's score carries the tension perfectly.","2026-05-17"),
            ("r22","u6","m6",7,"Clever, but a bit cold emotionally.","2026-06-08"),
            ("r23","u3","m7",10,"Every frame is a painting. Fiennes is hilarious.","2026-06-20"),
            ("r24","u5","m7",8,"Delightfully whimsical, though the story is thinner than the visuals.","2026-07-05"),
            ("r25","u4","m8",9,"The tension builds so slowly you don't notice until it's unbearable.","2026-08-06"),
            ("r26","u6","m8",8,"Smart social commentary wrapped in a genuinely creepy thriller.","2026-08-10"),
            ("r27","u7","m8",9,"Peele nails the tone shift from funny to horrifying.","2026-08-13"),
            ("r28","u5","m9",9,"The ending recontextualizes the whole film. Gorgeous score.","2026-04-25"),
            ("r29","u3","m9",7,"Beautifully shot, though the story leans a little thin.","2026-05-14"),
            ("r30","u2","m10",9,"Practically one long chase and it never drags. Incredible stunt work.","2026-05-10"),
            ("r31","u6","m10",9,"Furiosa steals the entire film.","2026-05-28"),
            ("r32","u4","m10",8,"Simple story, but the craft is next level.","2026-06-15"),
            ("r33","u2","m11",10,"The best hope-in-adversity story ever put to film.","2026-03-20"),
            ("r34","u5","m11",10,"Morgan Freeman's narration ties it all together beautifully.","2026-04-11"),
            ("r35","u7","m11",9,"Slow burn, but the payoff is enormous.","2026-05-03"),
            ("r36","u1","m11",9,"A deserved classic.","2026-07-01"),
            ("r37","u3","m12",9,"Whimsical without being saccharine. Tautou is perfect in the role.","2026-07-08"),
            ("r38","u7","m12",8,"Charming, though the narrator leans on quirk a bit much.","2026-07-19"),
            ("r39","u4","m13",9,"Bardem's performance is unsettling in the best way.","2026-07-02"),
            ("r40","u2","m13",8,"Bleak and methodical. Not for everyone, but expertly made.","2026-07-15"),
            ("r41","u2","m14",9,"The docking sequence is the tensest ten minutes I've seen in a theater.","2026-04-12"),
            ("r42","u4","m14",7,"Emotionally overwrought in places, but visually stunning.","2026-04-27"),
            ("r43","u5","m14",9,"That score. That ending. I was a mess.","2026-05-19"),
            ("r44","u6","m14",8,"Ambitious, and mostly sticks the landing.","2026-06-02"),
            ("r45","u5","m15",10,"J.K. Simmons is terrifying. The final drum solo is unreal.","2026-08-11"),
            ("r46","u3","m15",9,"Stressful in the best way. My palms were sweating.","2026-08-14"),
            ("r47","u6","m15",9,"Tight, tense, and the editing is razor sharp.","2026-08-16"),
            ("r48","u7","m16",9,"Genuinely surprising whodunit with a great cast having fun.","2026-07-28"),
            ("r49","u4","m16",8,"Sharp script, and de Armas carries the emotional core.","2026-08-01"),
            ("r50","u3","m17",10,"Chaotic in the best possible way. The multiverse hot-dog-fingers bit still makes me laugh.","2026-08-15"),
            ("r51","u6","m17",9,"Somehow both absurd and deeply moving.","2026-08-16"),
            ("r52","u2","m17",9,"The editing is a masterclass in controlled chaos.","2026-08-17"),
            ("r53","u2","m18",9,"The Trinity test sequence is unbearable tension without a single effects shot wasted.","2026-08-16"),
            ("r54","u5","m18",8,"Dense and talky, but Murphy's performance carries it.","2026-08-17")
        };

        var reviews = new Dictionary<string, Review>();
        foreach (var r in reviewData)
        {
            var review = new Review
            {
                User = users[r.User],
                Movie = movies[r.Movie],
                Rating = r.Rating,
                Text = r.Text,
                CreatedAt = D(r.Date)
            };
            reviews[r.Key] = review;
            db.Reviews.Add(review);
        }

        // KOMENTARI
        var commentData = new (string Review, string User, string Text, string Date)[]
        {
            ("r1","u3","Completely agree, the wedding scene alone sets up the whole film.","2026-05-03"),
            ("r1","u6","Still haven't made it through part III though.","2026-05-04"),
            ("r9","u4","Bullet time really was a technical leap for its time.","2026-04-06"),
            ("r19","u5","The hallway fight scene still blows my mind.","2026-04-03"),
            ("r33","u7","Freeman's narration makes this one rewatchable forever.","2026-03-21"),
            ("r14","u2","The tonal shift halfway through caught me off guard too.","2026-07-21"),
            ("r25","u6","The teacup scene still gives me chills.","2026-08-07"),
            ("r41","u4","Docking scene had my heart racing, agreed.","2026-04-13"),
            ("r45","u3","That final scene is unbelievably tense.","2026-08-12"),
            ("r50","u7","The hot dog universe is unhinged in the best way.","2026-08-15"),
            ("r39","u2","Bardem's coin flip scene is one of the best in the genre.","2026-07-03"),
            ("r30","u5","Agreed, barely any dialogue needed.","2026-05-11"),
            ("r5","u6","Same, the diner opening still hooks me every time.","2026-04-16")
        };

        foreach (var c in commentData)
        {
            db.Comments.Add(new Comment
            {
                Review = reviews[c.Review],
                User = users[c.User],
                Text = c.Text,
                CreatedAt = D(c.Date)
            });
        }

        // FAVORITI
        var favoriteData = new (string User, string Movie)[]
        {
            ("u2","m3"),("u2","m6"),("u2","m11"),("u2","m14"),("u2","m17"),
            ("u3","m4"),("u3","m5"),("u3","m7"),("u3","m17"),
            ("u4","m2"),("u4","m8"),("u4","m13"),
            ("u5","m9"),("u5","m11"),("u5","m15"),
            ("u6","m10"),("u6","m4"),
            ("u7","m5"),("u7","m12"),("u7","m16"),
            ("u1","m3")
        };

        foreach (var f in favoriteData)
        {
            db.Favorites.Add(new Favorite
            {
                User = users[f.User],
                Movie = movies[f.Movie],
                CreatedAt = DateTime.UtcNow
            });
        }

        db.SaveChanges();
    }

    /// <summary>Popunjava PosterUrl za poznate naslove kojima jos nedostaje slika (TMDB posteri).</summary>
    private static void BackfillPosters(ApplicationDbContext db)
    {
        var posters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["The Godfather"] = PosterBase + "/3bhkrj58Vtu7enYsRolD1fZdja1.jpg",
            ["Pulp Fiction"] = PosterBase + "/vQWk5YBFWF4bZaofAbv0tShwBvQ.jpg",
            ["The Matrix"] = PosterBase + "/dXNAPwY7VrqMAo51EKhhCJfaGb5.jpg",
            ["Parasite"] = PosterBase + "/7IiTTgloJzvGI1TAYymCfbfl3vT.jpg",
            ["Spirited Away"] = PosterBase + "/39wmItIWsg5sZMyRUHLkWBcuVCM.jpg",
            ["Inception"] = PosterBase + "/xlaY2zyzMfkhk0HSC5VUwzoZPU1.jpg",
            ["The Grand Budapest Hotel"] = PosterBase + "/eWdyYQreja6JGCzqHWXpWHDrrPo.jpg",
            ["Get Out"] = PosterBase + "/tFXcEccSQMf3lfhfXKSU9iRBpa3.jpg",
            ["La La Land"] = PosterBase + "/uDO8zWDhfWwoFdKS4fzkUJt0Rf0.jpg",
            ["Mad Max: Fury Road"] = PosterBase + "/ulcAi4dKpAjHwYGS08vNyx9H6I9.jpg",
            ["The Shawshank Redemption"] = PosterBase + "/9cqNxx0GxF0bflZmeSMuL5tnGzr.jpg",
            ["Amelie"] = PosterBase + "/nSxDa3M9aMvGVLoItzWTepQ5h5d.jpg",
            ["No Country for Old Men"] = PosterBase + "/6d5XOczc226jECq0LIX0siKtgHR.jpg",
            ["Interstellar"] = PosterBase + "/yQvGrMoipbRoddT0ZR8tPoR7NfX.jpg",
            ["Whiplash"] = PosterBase + "/7fn624j5lj3xTme2SgiLCeuedmO.jpg",
            ["Knives Out"] = PosterBase + "/pThyQovXQrw2m0s9x82twj48Jq4.jpg",
            ["Everything Everywhere All at Once"] = PosterBase + "/u68AjlvlutfEIcpmbYpKcdi09ut.jpg",
            ["Oppenheimer"] = PosterBase + "/8Gxv8gSFCU0XGDykEGv7zR1n2ua.jpg"
        };

        var missing = db.Movies.Where(m => m.PosterUrl == null || m.PosterUrl == "").ToList();
        if (missing.Count == 0) return;

        var changed = false;
        foreach (var movie in missing)
        {
            if (posters.TryGetValue(movie.Title, out var url))
            {
                movie.PosterUrl = url;
                changed = true;
            }
        }

        if (changed) db.SaveChanges();
    }
}
