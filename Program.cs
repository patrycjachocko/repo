using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using praca_dyplomowa_zesp.Data;
using praca_dyplomowa_zesp.Models.API;
using praca_dyplomowa_zesp.Models.Modules.Users;
using praca_dyplomowa_zesp.Services;
using Rotativa.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- KONFIGURACJA BAZY DANYCH ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// --- KONFIGURACJA SYSTEMU IDENTITY ---
builder.Services.AddIdentity<User, IdentityRole<Guid>>(options =>
{
    // Polityka hase³
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    // Mechanizm blokady konta po nieudanych próbach logowania
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Konfiguracja œcie¿ek dostêpu dla ciasteczek autoryzacyjnych
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// --- REJESTRACJA US£UG SYSTEMOWYCH I MODU£ÓW ---
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddHostedService<TrashCleanupService>(); //serwis czyszcz¹cy kosz w tle

// --- INTEGRACJE Z ZEWNÊTRZNYMI API ---

// Klient IGDB
builder.Services.AddSingleton<IGDBClient>(provider =>
{
    var clientId = builder.Configuration["IGDB:ClientId"];
    var clientSecret = builder.Configuration["IGDB:ClientSecret"];
    return new IGDBClient(clientId, clientSecret);
});

// Serwis Steam
builder.Services.AddHttpClient<SteamApiService>();

// Autentykacja Steam (OpenID)
builder.Services.AddAuthentication()
    .AddSteam(options =>
    {
        options.CorrelationCookie.SameSite = SameSiteMode.None;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
    });

var app = builder.Build();

// --- INICJALIZACJA DANYCH (SEEDING) ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<User>>();

        // Definicja poziomów uprawnieñ w systemie
        string[] roleNames = { "Admin", "Moderator", "User" };

        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            }
        }

        // Automatyczne tworzenie konta administratora przy pierwszym uruchomieniu
        var adminUser = await userManager.FindByNameAsync("Admin");
        if (adminUser == null)
        {
            var newAdmin = new User
            {
                UserName = "Admin",
                Login = "admin",
                Role = "Admin",
                CreatedAt = DateTime.Now
            };

            var result = await userManager.CreateAsync(newAdmin, "Admin890");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(newAdmin, "Admin");
            }
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "B³¹d krytyczny podczas inicjalizacji ról lub konta administratora.");
    }
}

// --- POTOK PRZETWARZANIA (MIDDLEWARE) ---
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

// Konfiguracja narzêdzia Rotativa do generowania raportów PDF
RotativaConfiguration.Setup(app.Environment.WebRootPath, "Rotativa");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();