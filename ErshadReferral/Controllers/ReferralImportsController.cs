using ErshadReferral.Models;
using ErshadReferral.Models.ViewModels;
using ErshadReferral.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ErshadReferral.Controllers;

[Authorize(Roles = $"{AppRoles.IT},{AppRoles.ErshadManager}")]
public class ReferralImportsController : Controller
{
    private const long MaxUploadSize = 20 * 1024 * 1024;
    private readonly ReferralExcelImportService _importService;
    private readonly UserManager<AppUser> _userManager;

    public ReferralImportsController(
        ReferralExcelImportService importService,
        UserManager<AppUser> userManager)
    {
        _importService = importService;
        _userManager = userManager;
    }

    [HttpGet]
    public IActionResult Index() => View(new ReferralExcelImportViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxUploadSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadSize)]
    public async Task<IActionResult> Index(ReferralExcelImportViewModel model)
    {
        if (model.File == null || model.File.Length == 0)
        {
            model.ErrorMessage = "فایل اکسل را انتخاب کن.";
            return View(model);
        }

        if (!string.Equals(Path.GetExtension(model.File.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            model.ErrorMessage = "فقط فایل با پسوند xlsx قابل قبول است.";
            return View(model);
        }

        if (model.File.Length > MaxUploadSize)
        {
            model.ErrorMessage = "حجم فایل نباید از ۲۰ مگابایت بیشتر باشد.";
            return View(model);
        }

        var referralOwner = await _userManager.FindByNameAsync("voroodi");
        if (referralOwner == null)
        {
            model.ErrorMessage = "کاربر voroodi در سامانه پیدا نشد؛ ارجاعی ثبت نشد.";
            return View(model);
        }

        try
        {
            await using var input = model.File.OpenReadStream();
            await using var buffer = new MemoryStream((int)model.File.Length);
            await input.CopyToAsync(buffer);

            var result = await _importService.ImportAsync(
                buffer.ToArray(),
                Path.GetFileName(model.File.FileName),
                User.Identity?.Name,
                referralOwner);

            model.Summary = new ReferralExcelImportSummary
            {
                TotalRows = result.TotalRows,
                ImportedRows = result.ImportedRows,
                SkippedDuplicateRows = result.SkippedDuplicateRows,
                NewPeople = result.NewPeople,
                ReusedPeople = result.ReusedPeople,
                MappedBranchRows = result.MappedBranchRows,
                UnmappedBranchRows = result.UnmappedBranchRows,
                MappedSubjectRows = result.MappedSubjectRows,
                UnmappedSubjectRows = result.UnmappedSubjectRows,
                InvalidNationalCodeRows = result.InvalidNationalCodeRows,
                MissingLastNameRows = result.MissingLastNameRows
            };
        }
        catch (ReferralExcelImportException ex)
        {
            model.ErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            model.ErrorMessage = "واردسازی انجام نشد؛ اطلاعات ثبت نشدند. فایل یا ارتباط دیتابیس را بررسی کن.";
        }

        model.File = null;
        ModelState.Clear();
        return View(model);
    }
}
