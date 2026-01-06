using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using praca_dyplomowa.Data;
using praca_dyplomowa_zesp;
using praca_dyplomowa_zesp.Models.API;
using praca_dyplomowa_zesp.Models.Users;
using praca_dyplomowa_zesp.Services;
using Rotativa.AspNetCore;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// Konfiguracja Identity z obs³ug¹ Ról
builder.Services.AddIdentity<User, IdentityRole<Guid>>(options =>
{
    // Ustawienia has³a
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    // Ustawienia blokady (Lockout)
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    options.SignIn.RequireConfirmedAccount = false;

    // --- KLUCZOWA ZMIANA: Pozwalamy na brak unikalnego maila (lub brak maila w ogóle) ---
    options.User.RequireUniqueEmail = false;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();


builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddControllersWithViews();

builder.Services.AddHostedService<TrashCleanupService>();

// --- KONFIGURACJA ZEWNÊTRZNYCH API ---

// 1. IGDB
builder.Services.AddSingleton<IGDBClient>(provider =>
{
    var clientId = builder.Configuration["IGDB:ClientId"];
    var clientSecret = builder.Configuration["IGDB:ClientSecret"];

    if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
    {
        throw new Exception("Klucze ClientId i ClientSecret dla IGDB nie zosta³y ustawione w appsettings.json");
    }

    return new IGDBClient(clientId, clientSecret);
});

// 2. STEAM - Rejestracja serwisu API
builder.Services.AddHttpClient<SteamApiService>();

// 3. STEAM - Konfiguracja logowania (OpenID)
builder.Services.AddAuthentication()
    .AddSteam(options =>
    {
        options.CorrelationCookie.SameSite = SameSiteMode.None;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
    });

// -------------------------------------

builder.Services.AddMemoryCache();

var app = builder.Build();

// --- SEEDOWANIE RÓL I ADMINA ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<User>>();

        // 1. Tworzenie ról (ZMIANA: Dodano "Verified")
        string[] roleNames = { "Admin", "Moderator", "User" };

        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            }
        }

        // 2. Tworzenie Admina, jeœli nie istnieje
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
        logger.LogError(ex, "Wyst¹pi³ b³¹d podczas tworzenia ról lub konta administratora.");
    }
}
// ----------------------------------------

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

RotativaConfiguration.Setup(app.Environment.WebRootPath, "Rotativa");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();