using ErshadReferral.Data;
using ErshadReferral.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ErshadReferral.Controllers;

[Authorize(Roles = AppRoles.IT)]
public class BranchesController : Controller
{
    private readonly ApplicationDbContext _db;

    public BranchesController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var branches = await _db.Branches.Include(b => b.Court)
            .OrderBy(b => b.Court!.Name).ThenBy(b => b.Name).ToListAsync();
        return View(branches);
    }

    public async Task<IActionResult> Create()
    {
        await LoadCourtsAsync();
        return View(new Branch());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Branch branch)
    {
        ModelState.Remove(nameof(Branch.Court));
        if (!ModelState.IsValid)
        {
            await LoadCourtsAsync();
            return View(branch);
        }
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();
        TempData["Success"] = "شعبه با موفقیت ثبت شد";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var branch = await _db.Branches.FindAsync(id);
        if (branch == null) return NotFound();
        await LoadCourtsAsync();
        return View(branch);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Branch branch)
    {
        if (id != branch.Id) return NotFound();
        ModelState.Remove(nameof(Branch.Court));
        if (!ModelState.IsValid)
        {
            await LoadCourtsAsync();
            return View(branch);
        }
        _db.Update(branch);
        await _db.SaveChangesAsync();
        TempData["Success"] = "شعبه با موفقیت ویرایش شد";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var branch = await _db.Branches.FindAsync(id);
        if (branch == null) return NotFound();
        branch.IsActive = !branch.IsActive;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadCourtsAsync()
    {
        ViewBag.Courts = new SelectList(
            await _db.Courts.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(),
            "Id", "Name");
    }
}
