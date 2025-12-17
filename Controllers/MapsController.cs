using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using praca_dyplomowa.Data;
using praca_dyplomowa_zesp.Models.Modules.Guides;
using praca_dyplomowa_zesp.Models.Users;

namespace praca_dyplomowa_zesp.Controllers
{
    [Authorize]
    public class MapsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IWebHostEnvironment _environment;

        public MapsController(ApplicationDbContext context, UserManager<User> userManager, IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
        }

        // 1. Pobierz listę map dla danej gry (do wyświetlenia w modalu)
        [HttpGet]
        public async Task<IActionResult> GetMapsForGame(long gameId, string? searchString)
        {
            var query = _context.GameMaps.Where(m => m.IgdbGameId == gameId);

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(m => m.Name.ToLower().Contains(searchString.ToLower()));
            }

            var maps = await query.Select(m => new
            {
                m.Id,
                m.Name,
                m.ImageUrl
            }).ToListAsync();

            return Json(maps);
        }

        // 2. Upload nowej mapy
        [HttpPost]
        public async Task<IActionResult> UploadMap(long gameId, string name, IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("Nie wybrano pliku.");
            if (string.IsNullOrWhiteSpace(name)) return BadRequest("Podaj nazwę mapy.");

            // Zapis pliku na serwerze
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "maps");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            var user = await _userManager.GetUserAsync(User);

            // Zapis w bazie
            var newMap = new GameMap
            {
                IgdbGameId = gameId,
                Name = name,
                ImageUrl = "/uploads/maps/" + uniqueFileName,
                UploadedByUserId = user.Id,
                CreatedAt = DateTime.Now
            };

            _context.GameMaps.Add(newMap);
            await _context.SaveChangesAsync();

            return Json(new { success = true, map = new { newMap.Id, newMap.Name, newMap.ImageUrl } });
        }
    }
}