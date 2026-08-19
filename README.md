# REEL — Movie Review Platform (ASP.NET Core MVC)

Backend i cjelovita web aplikacija izrađena prema HTML prototipu `Movie_Review_Platform_dc.html`.
Tehnologije: **ASP.NET Core 8 MVC**, **Entity Framework Core 8**, **SQLite**, cookie autentikacija.

---

## Pokretanje

Prije prvog pokretanja postavi tajne podatke preko `dotnet user-secrets` (ne drže se u kodu ni u
appsettings.json, pa se ne mogu slucajno zavrsiti u git repozitoriju):

```bash
cd MovieReviewPlatform
dotnet user-secrets init
dotnet user-secrets set "Tmdb:ApiKey" "<tvoj TMDB API kljuc>"
dotnet user-secrets set "Seed:AdminPassword" "<lozinka za admin racun>"
dotnet user-secrets set "Seed:UserPassword" "<lozinka za ostale demo racune>"

dotnet restore
dotnet run
```

Otvori `https://localhost:7090` (ili `http://localhost:5090`).

Baza (`reel.db`) kreira se automatski pri prvom pokretanju i puni se demo podacima iz prototipa
(18 filmova, 7 korisnika, 54 recenzije, 13 komentara, 21 favorit), plus dodatnim filmovima s TMDB-a.

### Demo računi

| Korisničko ime | Lozinka | Uloga |
|---|---|---|
| `admin` | vrijednost `Seed:AdminPassword` | Admin |
| `marcus_h`, `ana_r`, `ivan_b`, `petra_n`, `luke_b`, `emma_c` | vrijednost `Seed:UserPassword` | User |

---

## Struktura projekta

```
MovieReviewPlatform/
├── Models/Entities.cs              # domenski model (User, Movie, Genre, Review, Comment, Favorite)
├── Data/
│   ├── ApplicationDbContext.cs     # EF Core kontekst, relacije, indeksi
│   └── DbSeeder.cs                 # početni podaci
├── Services/                       # poslovni sloj (sučelja + implementacije)
│   ├── MovieService.cs
│   ├── ReviewService.cs
│   ├── UserService.cs
│   └── FavoriteAndStatsServices.cs
├── ViewModels/                     # modeli pogleda i forme s validacijom
├── Controllers/
│   ├── BaseController.cs           # trenutni korisnik, TempData poruke
│   ├── HomeController / MoviesController / RankingsController / FavoritesController
│   ├── AccountController           # prijava, registracija, odjava
│   ├── ProfileController / ReviewsController
│   └── AdminController
├── Views/                          # Razor pogledi
├── wwwroot/css/                    # dizajn sustav iz prototipa, rastavljen po cjelinama
│   ├── base.css                    # varijable, reset, tipografija
│   ├── layout.css                  # stranica, navigacija, sekcije, podnozje
│   ├── buttons-forms.css           # gumbi, polja formi, filteri
│   ├── components.css              # oznake, kartice, tablice, tabovi, obavijesti
│   ├── movies.css                  # kartica filma, mreza filmova, stranica detalja
│   └── reviews-rankings.css        # recenzije, komentari, rang liste
└── Program.cs                      # DI, autentikacija, rutiranje, init baze
```

Arhitektura je klasična troslojna: **kontroler → servis → DbContext**. Kontroleri ne pristupaju
bazi izravno, servisi vraćaju gotove ViewModele, a entiteti se nikad ne šalju direktno u pogled.

---

## Model podataka

```
User ──< Review >── Movie
  │        │           │
  │        └──< Comment│
  └──< Favorite >──────┘

Movie >──< Genre   (M:N preko MovieGenre)
```

Ograničenja postavljena u `ApplicationDbContext`:

- `Username` i `Email` su jedinstveni
- `(UserId, MovieId)` jedinstven na `Review` → jedan korisnik može ostaviti **najviše jednu**
  recenziju po filmu (novo slanje ažurira postojeću)
- `(UserId, MovieId)` jedinstven na `Favorite`
- brisanje filma/korisnika kaskadno briše recenzije i favorite
- `Comment.UserId` koristi `Restrict` (inače nastaje dvostruki kaskadni put koji SQL Server
  odbija), pa se komentari brišu ručno u `UserService` i `MovieService`

---

## Prijenos prototipa u MVC

| Prototip | Implementacija |
|---|---|
| `sc-if` / `sc-for` | `@if` / `@foreach` u Razoru |
| `dc-import name="MovieCard"` | `Views/Shared/_MovieCard.cshtml` |
| `state.screen` prebacivanje | stvarne rute i kontroleri |
| modal za film / potvrde | zasebna stranica `Admin/MovieForm` + `confirm()` na formi |
| tabovi (radio inputi) | linkovi s `?tab=` parametrom |
| `submitLogin` bez lozinke | prava provjera lozinke (`PasswordHasher<User>`) |
| `avgRating`, `popularityScore` | `MovieService.Average` / `MovieService.Popularity` |
| `computeRecommendations` | `MovieService.Recommend` — zbroj žanrova iz filmova ocijenjenih 7+ |
| `computeSimilar` | preklapanje žanrova × 10 + prosječna ocjena |
| `getFilteredMovies` | `MovieService.GetCatalogAsync` |

**Uklonjeno:** traka „Preview as Guest/User/Admin". To je bio prototipski prekidač uloga; sada
uloga dolazi iz autentikacijskog kolačića (`ClaimTypes.Role`).

---

## Rute

| Metoda | Ruta | Opis |
|---|---|---|
| GET | `/` | naslovnica |
| GET | `/Movies?q=&genre=&yearRange=&minRating=&sort=` | katalog s filterima |
| GET | `/Movies/Details/{id}` | detalji filma |
| POST | `/Reviews/Save` | dodaj ili uredi vlastitu recenziju |
| POST | `/Reviews/Delete` | briše autor ili admin |
| POST | `/Reviews/AddComment`, `/Reviews/DeleteComment` | komentari |
| POST | `/Favorites/Toggle` | dodaj/ukloni favorit |
| GET/POST | `/Account/Login`, `/Account/Register`, `/Account/Logout` | autentikacija |
| GET | `/Profile?tab=reviews\|rated\|favorites` | profil |
| GET/POST | `/Profile/Edit` | uređivanje profila |
| GET | `/Rankings` | rang liste |
| GET | `/Admin?tab=movies\|users\|reviews\|stats` | admin panel |
| GET/POST | `/Admin/CreateMovie`, `/Admin/EditMovie` | CRUD filmova |
| POST | `/Admin/DeleteMovie`, `/Admin/ToggleRole`, `/Admin/DeleteUser`, `/Admin/DeleteReview` | admin akcije |

---

## Sigurnost

- lozinke hashirane s `PasswordHasher<User>` (PBKDF2), nikad u čistom obliku
- svi POST-ovi imaju `@Html.AntiForgeryToken()` + `[ValidateAntiForgeryToken]`
- admin dio zaštićen politikom `AdminOnly` (`[Authorize(Policy = "AdminOnly")]`)
- provjera vlasništva pri brisanju recenzija i komentara (autor ili admin)
- zadnji admin se ne može degradirati ni obrisati; korisnik ne može obrisati sam sebe
- validacija na serveru preko Data Annotations i `ModelState` (poruke se ispisuju uz polja);
  na klijentu za sada rade samo HTML5 atributi (`required`, `type="email"`, `type="number"`).
  Za nenametljivu jQuery validaciju dodaj `_ValidationScriptsPartial.cshtml` i pozovi ga u
  sekciji `Scripts` na formama.

---

## Migracije

Zadano se koristi `EnsureCreated()` da aplikacija radi odmah, bez EF alata. Ako trebaš prave
migracije (npr. ako je to zahtjev na kolegiju):

1. u `Program.cs` zamijeni `db.Database.EnsureCreated();` s `db.Database.Migrate();`
2. obriši `reel.db` ako već postoji
3. pokreni:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## Prelazak na SQL Server LocalDB

1. u `MovieReviewPlatform.csproj` odkomentiraj `Microsoft.EntityFrameworkCore.SqlServer`
2. u `Program.cs`: `options.UseSqlite(...)` → `options.UseSqlServer(...)`
3. u konfiguraciji koristi `SqlServerConnection` umjesto `DefaultConnection`

---

## Moguća proširenja

- upload postera umjesto URL-a (`IFormFile` + `wwwroot/uploads`)
- straničenje kataloga (trenutno se filtrira u memoriji — dovoljno za ovaj broj filmova,
  ali kod tisuća zapisa treba prebaciti filtriranje i sortiranje u SQL)
- REST API kontroleri uz MVC, ako zatreba i klijentska aplikacija
