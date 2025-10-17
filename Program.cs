using Microsoft.EntityFrameworkCore;
using praca_dyplomowa.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IGDBClient>(provider =>
{
    var clientId = builder.Configuration["IGDB:ClientId"];
    var clientSecret = builder.Configuration["IGDB:ClientSecret"];

    if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
    {
        throw new System.Exception("Klucze ClientId i ClientSecret dla IGDB nie zosta³y ustawione w appsettings.json");
    }

    return new IGDBClient(clientId, clientSecret);
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
