using ErshadReferral.Data;
using ErshadReferral.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErshadReferral.Controllers;

[Authorize(Roles = AppRoles.IT)]
public class CourtsController : Controller
{
    private readonly ApplicationDbContext _db;

    public CourtsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var courts = await _db.Courts.Include(c => c.Branches).OrderBy(c => c.Name).ToListAsync();
        return View(courts);
    }

    public IActionResult Create()
    {
        return View(new Court());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Court court)
    {
        if (!ModelState.IsValid) return View(court);
        _db.Courts.Add(court);
        await _db.SaveChangesAsync();
        TempData["Success"] = "دادگاه با موفقیت ثبت شد";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var court = await _db.Courts.FindAsync(id);
        if (court == null) return NotFound();
        return View(court);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Court court)
    {
        if (id != court.Id) return NotFound();
        if (!ModelState.IsValid) return View(court);
        _db.Update(court);
        await _db.SaveChangesAsync();
        TempData["Success"] = "دادگاه با موفقیت ویرایش شد";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var court = await _db.Courts.FindAsync(id);
        if (court == null) return NotFound();
        court.IsActive = !court.IsActive;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
