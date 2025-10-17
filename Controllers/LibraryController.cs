using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using praca_dyplomowa.Data;

namespace praca_dyplomowa_zesp.Controllers
{
    public class LibraryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LibraryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Library
        public async Task<IActionResult> Index()
        {
            return View(await _context.GamesInLibraries.ToListAsync());
        }

        // GET: Library/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var gameInLibrary = await _context.GamesInLibraries
                .FirstOrDefaultAsync(m => m.Id == id);
            if (gameInLibrary == null)
            {
                return NotFound();
            }

            return View(gameInLibrary);
        }

        // GET: Library/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Library/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,IgdbGameId,UserId,DateAddedToLibrary,CurrentUserStoryMission,CurrentUserStoryProgressPercent")] GameInLibrary gameInLibrary)
        {
            if (ModelState.IsValid)
            {
                _context.Add(gameInLibrary);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(gameInLibrary);
        }

        // GET: Library/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var gameInLibrary = await _context.GamesInLibraries.FindAsync(id);
            if (gameInLibrary == null)
            {
                return NotFound();
            }
            return View(gameInLibrary);
        }

        // POST: Library/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,IgdbGameId,UserId,DateAddedToLibrary,CurrentUserStoryMission,CurrentUserStoryProgressPercent")] GameInLibrary gameInLibrary)
        {
            if (id != gameInLibrary.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(gameInLibrary);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!GameInLibraryExists(gameInLibrary.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(gameInLibrary);
        }

        // GET: Library/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var gameInLibrary = await _context.GamesInLibraries
                .FirstOrDefaultAsync(m => m.Id == id);
            if (gameInLibrary == null)
            {
                return NotFound();
            }

            return View(gameInLibrary);
        }

        // POST: Library/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var gameInLibrary = await _context.GamesInLibraries.FindAsync(id);
            if (gameInLibrary != null)
            {
                _context.GamesInLibraries.Remove(gameInLibrary);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool GameInLibraryExists(int id)
        {
            return _context.GamesInLibraries.Any(e => e.Id == id);
        }
    }
}
