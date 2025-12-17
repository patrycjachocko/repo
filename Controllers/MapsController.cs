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

        // 1. Dla zwykłego użytkownika (tylko aktywne mapy)
        [HttpGet]
        public async Task<IActionResult> GetMapsForGame(long gameId, string? searchString)
        {
            var query = _context.GameMaps
                .Where(m => m.IgdbGameId == gameId && !m.IsDeleted); // TYLKO NIEUSUNIĘTE

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

        // 2. Dla Admina/Moderatora (wszystkie mapy do zarządzania)
        [HttpGet]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> GetMapsForManager(long gameId)
        {
            var maps = await _context.GameMaps
                .Where(m => m.IgdbGameId == gameId)
                .OrderBy(m => m.IsDeleted) // Najpierw aktywne
                .ThenByDescending(m => m.Id)
                .Select(m => new
                {
                    m.Id,
                    m.Name,
                    m.ImageUrl,
                    m.IsDeleted
                })
                .ToListAsync();

            return Json(maps);
        }

        // 3. Zmiana nazwy (Admin/Mod)
        [HttpPost]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> RenameMap(int id, string newName)
        {
            var map = await _context.GameMaps.FindAsync(id);
            if (map == null) return NotFound();

            if (string.IsNullOrWhiteSpace(newName)) return BadRequest("Nazwa nie może być pusta");

            map.Name = newName;
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        // 4. Usuwanie/Przywracanie (Soft Delete)
        [HttpPost]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> ToggleMapStatus(int id)
        {
            var map = await _context.GameMaps.FindAsync(id);
            if (map == null) return NotFound();

            // Odwracamy status (jak było false to true, jak true to false)
            map.IsDeleted = !map.IsDeleted;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, isDeleted = map.IsDeleted });
        }

        // 5. Upload nowej mapy
        [HttpPost]
        public async Task<IActionResult> UploadMap(long gameId, string name, IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("Nie wybrano pliku.");
            if (string.IsNullOrWhiteSpace(name)) return BadRequest("Podaj nazwę mapy.");

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "maps");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            var user = await _userManager.GetUserAsync(User);

            var newMap = new GameMap
            {
                IgdbGameId = gameId,
                Name = name,
                ImageUrl = "/uploads/maps/" + uniqueFileName,
                UploadedByUserId = user != null ? user.Id : null,
                IsDeleted = false
            };

            _context.GameMaps.Add(newMap);
            await _context.SaveChangesAsync();

            return Json(new { success = true, map = new { newMap.Id, newMap.Name, newMap.ImageUrl } });
        }
    }
}