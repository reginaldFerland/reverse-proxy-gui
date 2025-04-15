using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReverseProxy.Data;
using ReverseProxy.Models;
using WebApp.Services;
using System.Text.Json;
using System.Text;

namespace WebApp.Controllers;

public class MappingsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ReverseProxyService _reverseProxyService;

    public MappingsController(ApplicationDbContext context, ReverseProxyService reverseProxyService)
    {
        _context = context;
        _reverseProxyService = reverseProxyService;
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

            // Trigger reverse proxy reload
            var reloadSuccess = await _reverseProxyService.ReloadConfigurationAsync();
            if (reloadSuccess)
            {
                TempData["SuccessMessage"] = "Mapping created and proxy configuration reloaded successfully.";
            }
            else
            {
                TempData["WarningMessage"] = "Mapping created but proxy configuration reload failed.";
            }

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

                // Trigger reverse proxy reload
                var reloadSuccess = await _reverseProxyService.ReloadConfigurationAsync();
                if (reloadSuccess)
                {
                    TempData["SuccessMessage"] = "Mapping updated and proxy configuration reloaded successfully.";
                }
                else
                {
                    TempData["WarningMessage"] = "Mapping updated but proxy configuration reload failed.";
                }
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

            // Trigger reverse proxy reload
            var reloadSuccess = await _reverseProxyService.ReloadConfigurationAsync();
            if (reloadSuccess)
            {
                TempData["SuccessMessage"] = "Mapping deleted and proxy configuration reloaded successfully.";
            }
            else
            {
                TempData["WarningMessage"] = "Mapping deleted but proxy configuration reload failed.";
            }
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: Mappings/ToggleDestination/5
    [HttpPost]
    public async Task<IActionResult> ToggleDestination(int id)
    {
        var mapping = await _context.Mappings.FindAsync(id);

        if (mapping == null)
        {
            return NotFound();
        }

        // Toggle between 1 and 2
        mapping.ActiveDestination = mapping.ActiveDestination == 1 ? 2 : 1;

        _context.Update(mapping);
        await _context.SaveChangesAsync();

        // Trigger reverse proxy reload
        var reloadSuccess = await _reverseProxyService.ReloadConfigurationAsync();
        if (reloadSuccess)
        {
            TempData["SuccessMessage"] = "Route destination toggled and proxy configuration reloaded successfully.";
        }
        else
        {
            TempData["WarningMessage"] = "Route destination toggled but proxy configuration reload failed.";
        }

        return RedirectToAction(nameof(Index));
    }

    // GET: Mappings/ReloadProxyConfig
    public async Task<IActionResult> ReloadProxyConfig()
    {
        // Trigger reverse proxy reload
        var reloadSuccess = await _reverseProxyService.ReloadConfigurationAsync();
        if (reloadSuccess)
        {
            TempData["SuccessMessage"] = "Proxy configuration reloaded successfully.";
        }
        else
        {
            TempData["WarningMessage"] = "Failed to reload proxy configuration.";
        }

        return RedirectToAction(nameof(Index));
    }

    // GET: Mappings/Export
    public async Task<IActionResult> Export()
    {
        var mappings = await _context.Mappings.ToListAsync();
        var json = JsonSerializer.Serialize(mappings, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        byte[] bytes = Encoding.UTF8.GetBytes(json);
        return File(bytes, "application/json", "mappings.json");
    }

    // GET: Mappings/Import
    public IActionResult Import()
    {
        return View();
    }

    // POST: Mappings/Import
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile jsonFile)
    {
        if (jsonFile == null || jsonFile.Length == 0)
        {
            TempData["WarningMessage"] = "No file was uploaded.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            using var streamReader = new StreamReader(jsonFile.OpenReadStream());
            var json = await streamReader.ReadToEndAsync();
            var mappings = JsonSerializer.Deserialize<List<Mapping>>(json);

            if (mappings == null || !mappings.Any())
            {
                TempData["WarningMessage"] = "No mappings found in the uploaded file.";
                return RedirectToAction(nameof(Index));
            }

            // Clear existing mappings
            _context.Mappings.RemoveRange(_context.Mappings);

            // Add new mappings (without preserving IDs)
            foreach (var mapping in mappings)
            {
                mapping.Id = 0; // Reset ID to let the database generate a new one
                _context.Mappings.Add(mapping);
            }

            await _context.SaveChangesAsync();

            // Trigger reverse proxy reload
            var reloadSuccess = await _reverseProxyService.ReloadConfigurationAsync();
            if (reloadSuccess)
            {
                TempData["SuccessMessage"] = $"Successfully imported {mappings.Count} mappings and reloaded proxy configuration.";
            }
            else
            {
                TempData["WarningMessage"] = $"Successfully imported {mappings.Count} mappings but proxy configuration reload failed.";
            }

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["WarningMessage"] = $"Error importing mappings: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }
}