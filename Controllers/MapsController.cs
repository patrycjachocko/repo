using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using praca_dyplomowa_zesp.Data;
using praca_dyplomowa_zesp.Models.Modules.Guides;
using praca_dyplomowa_zesp.Models.Modules.Users;

namespace praca_dyplomowa_zesp.Controllers
{
    [Authorize]
    public class MapsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IWebHostEnvironment _environment;

        public MapsController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IWebHostEnvironment environment)
        {
            //przypisanie wstrzyknietych serwisow do pol klasy
            _context = context;
            _userManager = userManager;
            _environment = environment;
        }

        #region Public Map Actions

        [HttpGet]
        public async Task<IActionResult> GetMapsForGame(long gameId, string? searchString)
        {
            //pobranie aktywnych map dla danej gry z pominieciem rekordow usunietych
            var query = _context.GameMaps
                .Where(m => m.IgdbGameId == gameId && !m.IsDeleted);

            if (!string.IsNullOrEmpty(searchString))
            {
                //filtrowanie wynikow po nazwie z ignorowaniem wielkosci liter
                var searchLower = searchString.ToLower();
                query = query.Where(m => m.Name.ToLower().Contains(searchLower));
            }

            var maps = await query.Select(m => new
            {
                m.Id,
                m.Name,
                m.ImageUrl
            }).ToListAsync();

            return Json(maps);
        }

        [HttpPost]
        public async Task<IActionResult> UploadMap(long gameId, string name, IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("Nie wybrano pliku.");
            if (string.IsNullOrWhiteSpace(name)) return BadRequest("Podaj nazwę mapy.");

            //wyznaczenie fizycznej sciezki do folderu zapisu na serwerze
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "maps");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            //zabezpieczenie przed nadpisaniem pliku poprzez dodanie unikalnego identyfikatora guid
            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                //skopiowanie zawartosci przeslanego pliku do strumienia zapisu
                await file.CopyToAsync(fileStream);
            }

            var user = await _userManager.GetUserAsync(User);

            var newMap = new GameMap
            {
                IgdbGameId = gameId,
                Name = name,
                //zapis sciezki wzglednej umozliwiajacej poprawne wyswietlanie w przegladarce
                ImageUrl = "/uploads/maps/" + uniqueFileName,
                UploadedByUserId = user?.Id,
                IsDeleted = false
            };

            _context.GameMaps.Add(newMap);
            await _context.SaveChangesAsync();

            return Json(new { success = true, map = new { newMap.Id, newMap.Name, newMap.ImageUrl } });
        }

        #endregion

        #region Management Actions (Admin/Moderator)

        [HttpGet]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> GetMapsForManager(long gameId)
        {
            //pobranie pelnej listy map dla moderatora w kolejnosci od najnowszych
            var maps = await _context.GameMaps
                .Where(m => m.IgdbGameId == gameId && !m.IsDeleted)
                .OrderByDescending(m => m.Id)
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

        [HttpPost]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> ToggleMapStatus(int id)
        {
            var map = await _context.GameMaps.FindAsync(id);
            if (map == null) return NotFound();

            //realizacja miekkiego usuwania rekordu poprzez zmiane flagi
            map.IsDeleted = true;

            if (!map.Name.StartsWith("[DELETED]"))
            {
                //zmiana nazwy na archiwalna z dodaniem unikalnego znacznika czasu ticks
                map.Name = $"[DELETED] {map.Name}_{DateTime.Now.Ticks}";
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, isDeleted = true });
        }

        #endregion
    }
}