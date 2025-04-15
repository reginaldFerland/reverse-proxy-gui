using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReverseProxy.Data;
using ReverseProxy.Models;

namespace WebApp.Controllers;

public class MappingsController : Controller
{
    private readonly ApplicationDbContext _context;

    public MappingsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Mappings
    public async Task<IActionResult> Index()
    {
        var mappings = await _context.Mappings.ToListAsync();
        return View(mappings);
    }

    // GET: Mappings/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var mapping = await _context.Mappings
            .FirstOrDefaultAsync(m => m.Id == id);

        if (mapping == null)
        {
            return NotFound();
        }

        return View(mapping);
    }

    // GET: Mappings/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Mappings/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,RoutePattern,Destination1,Destination2,ActiveDestination")] Mapping mapping)
    {
        if (ModelState.IsValid)
        {
            _context.Add(mapping);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(mapping);
    }

    // GET: Mappings/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var mapping = await _context.Mappings.FindAsync(id);

        if (mapping == null)
        {
            return NotFound();
        }

        return View(mapping);
    }

    // POST: Mappings/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,RoutePattern,Destination1,Destination2,ActiveDestination")] Mapping mapping)
    {
        if (id != mapping.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(mapping);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MappingExists(mapping.Id))
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
        return View(mapping);
    }

    private bool MappingExists(int id)
    {
        return _context.Mappings.Any(e => e.Id == id);
    }

    // GET: Mappings/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var mapping = await _context.Mappings
            .FirstOrDefaultAsync(m => m.Id == id);

        if (mapping == null)
        {
            return NotFound();
        }

        return View(mapping);
    }

    // POST: Mappings/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var mapping = await _context.Mappings.FindAsync(id);

        if (mapping != null)
        {
            _context.Mappings.Remove(mapping);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }
}