using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using praca_dyplomowa.Data;
using praca_dyplomowa_zesp.Models.Modules.Guides;
using praca_dyplomowa_zesp.Models.Users;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace praca_dyplomowa_zesp.Controllers
{
    /// <summary>
    /// Kontroler odpowiedzialny za zarządzanie mapami do gier (wgrywanie, listowanie, usuwanie).
    /// </summary>
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
            _context = context;
            _userManager = userManager;
            _environment = environment;
        }

        #region Public Map Actions

        /// <summary>
        /// Pobiera listę aktywnych (nieusuniętych) map dla konkretnej gry.
        /// </summary>
        /// <param name="gameId">ID gry z IGDB.</param>
        /// <param name="searchString">Opcjonalna fraza do filtrowania po nazwie.</param>
        [HttpGet]
        public async Task<IActionResult> GetMapsForGame(long gameId, string? searchString)
        {
            var query = _context.GameMaps
                .Where(m => m.IgdbGameId == gameId && !m.IsDeleted);

            if (!string.IsNullOrEmpty(searchString))
            {
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

        /// <summary>
        /// Obsługuje przesyłanie nowego pliku graficznego mapy na serwer i zapis w bazie.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UploadMap(long gameId, string name, IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("Nie wybrano pliku.");
            if (string.IsNullOrWhiteSpace(name)) return BadRequest("Podaj nazwę mapy.");

            // Przygotowanie ścieżki zapisu
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "maps");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            // Generowanie unikalnej nazwy pliku
            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
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
                UploadedByUserId = user?.Id,
                IsDeleted = false
            };

            _context.GameMaps.Add(newMap);
            await _context.SaveChangesAsync();

            return Json(new { success = true, map = new { newMap.Id, newMap.Name, newMap.ImageUrl } });
        }

        #endregion

        #region Management Actions (Admin/Moderator)

        /// <summary>
        /// Pobiera listę map przeznaczoną do panelu zarządzania (tylko dla Admina/Moderatora).
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> GetMapsForManager(long gameId)
        {
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

        /// <summary>
        /// Zmienia nazwę istniejącej mapy.
        /// </summary>
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

        /// <summary>
        /// Realizuje proces "miękkiego usuwania" mapy (Soft Delete).
        /// Oznacza mapę jako usuniętą i modyfikuje jej nazwę w celach archiwizacyjnych.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> ToggleMapStatus(int id)
        {
            var map = await _context.GameMaps.FindAsync(id);
            if (map == null) return NotFound();

            map.IsDeleted = true;

            // Oznaczanie nazwy jako usuniętej z unikalnym znacznikiem czasu
            if (!map.Name.StartsWith("[DELETED]"))
            {
                map.Name = $"[DELETED] {map.Name}_{DateTime.Now.Ticks}";
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, isDeleted = true });
        }

        #endregion
    }
}