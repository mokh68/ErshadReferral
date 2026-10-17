using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ErshadReferral.Data;
using ErshadReferral.Helpers;
using ErshadReferral.Models;
using Microsoft.EntityFrameworkCore;

namespace ErshadReferral.Services;

public sealed class ReferralExcelImportService
{
    private const int MaxFileBytes = 20 * 1024 * 1024;
    private const long MaxExpandedBytes = 60 * 1024 * 1024;
    private const string UnmappedBranchName = "ارجاعات وارداتی - شعبه نامشخص";
    private const string ImportedAppointmentSubject = "نوبت مراجعه";

    private static readonly XNamespace SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    private readonly ApplicationDbContext _db;

    public ReferralExcelImportService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ReferralExcelImportResult> ImportAsync(
        byte[] fileBytes,
        string fileName,
        string? uploadedByUserName,
        AppUser referralOwner)
    {
        if (fileBytes.Length == 0)
            throw new ReferralExcelImportException("فایل خالی است.");
        if (fileBytes.Length > MaxFileBytes)
            throw new ReferralExcelImportException("حجم فایل نباید از ۲۰ مگابایت بیشتر باشد.");

        var fileHash = Convert.ToHexString(SHA256.HashData(fileBytes));
        if (await _db.ReferralImportBatches.AnyAsync(b => b.FileSha256 == fileHash))
            throw new DuplicateReferralImportException("این فایل قبلاً وارد سامانه شده است.");

        var rows = ParseWorkbook(fileBytes);
        if (rows.Count == 0)
            throw new ReferralExcelImportException("فایل هیچ ردیف داده‌ای ندارد.");

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.NationalCode))
                throw new ReferralExcelImportException($"کد ملی در ردیف {row.ExcelRow} خالی است؛ فایل اصلاح و دوباره بارگذاری شود.");
            if (row.NationalCode.Length > 20)
                throw new ReferralExcelImportException($"کد ملی ردیف {row.ExcelRow} بیش از ۲۰ نویسه است و قابل ثبت نیست.");
            if (BuildDescription(row).Length > 1000)
                throw new ReferralExcelImportException($"اطلاعات ردیف {row.ExcelRow} از ظرفیت فیلد توضیحات بیشتر است.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            if (await _db.ReferralImportBatches.AnyAsync(b => b.FileSha256 == fileHash))
                throw new DuplicateReferralImportException("این فایل قبلاً وارد سامانه شده است.");

            var branches = await _db.Branches
                .Include(b => b.Court)
                .ToListAsync();
            var aliases = await _db.ReferralImportAliases.ToListAsync();
            var existingSubjects = await _db.Referrals
                .Select(r => r.Subject)
                .Distinct()
                .ToListAsync();

            var subjectCatalog = ReferralSubjects.Subjects
                .Concat(existingSubjects)
                .Append(ImportedAppointmentSubject)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .GroupBy(NormalizeForMatch)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            var fingerprints = rows.Select(ComputeFingerprint).Distinct(StringComparer.Ordinal).ToList();
            var knownFingerprints = (await _db.Referrals
                .Where(r => r.ImportFingerprint != null && fingerprints.Contains(r.ImportFingerprint))
                .Select(r => r.ImportFingerprint!)
                .ToListAsync())
                .ToHashSet(StringComparer.Ordinal);

            var seenInFile = new HashSet<string>(StringComparer.Ordinal);
            var uniqueRows = new List<AppointmentRow>();
            var skippedRows = 0;
            foreach (var row in rows)
            {
                var fingerprint = ComputeFingerprint(row);
                if (!seenInFile.Add(fingerprint) || knownFingerprints.Contains(fingerprint))
                {
                    skippedRows++;
                    continue;
                }
                uniqueRows.Add(row);
            }

            var existingCodes = uniqueRows
                .Select(r => r.NationalCode)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var peopleByCode = await _db.People
                .Where(p => existingCodes.Contains(p.NationalCode))
                .ToDictionaryAsync(p => p.NationalCode, StringComparer.Ordinal);

            var resolvedBranches = new Dictionary<string, Branch?>(StringComparer.Ordinal);
            var resolvedSubjects = new Dictionary<string, string?>(StringComparer.Ordinal);
            var newAliases = new List<ReferralImportAlias>();
            var needsFallbackBranch = uniqueRows.Any(row => ResolveBranch(row.BranchName, branches, aliases, resolvedBranches, newAliases) == null);
            Branch? fallbackBranch = null;
            if (needsFallbackBranch)
            {
                fallbackBranch = await _db.Branches.FirstOrDefaultAsync(b => b.Name == UnmappedBranchName);
                if (fallbackBranch == null)
                {
                    var courtId = branches.FirstOrDefault()?.CourtId ?? await _db.Courts
                        .OrderBy(c => c.Id)
                        .Select(c => (int?)c.Id)
                        .FirstOrDefaultAsync() ?? 0;
                    if (courtId == 0)
                        throw new ReferralExcelImportException("برای ثبت شعبهٔ نامشخص، ابتدا حداقل یک دادگاه در سامانه تعریف شود.");

                    fallbackBranch = new Branch
                    {
                        Name = UnmappedBranchName,
                        CourtId = courtId,
                        IsActive = false
                    };
                    _db.Branches.Add(fallbackBranch);
                }
            }

            var now = DateTime.Now;
            var persianYear = PersianDateHelper.GetPersianYear(now);
            var referralPrefix = $"{persianYear}-";
            var lastReferralNumber = await _db.Referrals
                .Where(r => r.ReferralNumber.StartsWith(referralPrefix))
                .OrderByDescending(r => r.ReferralNumber)
                .Select(r => r.ReferralNumber)
                .FirstOrDefaultAsync();
            var nextReferralNumber = 1;
            if (lastReferralNumber != null &&
                int.TryParse(lastReferralNumber.Substring(referralPrefix.Length), out var lastCounter))
                nextReferralNumber = lastCounter + 1;

            var batch = new ReferralImportBatch
            {
                FileSha256 = fileHash,
                FileName = Path.GetFileName(fileName)[..Math.Min(Path.GetFileName(fileName).Length, 260)],
                ImportedAt = now,
                UploadedByUserName = uploadedByUserName,
                TotalRows = rows.Count,
                SkippedDuplicateRows = skippedRows
            };

            var newPeople = 0;
            var reusedPeople = 0;
            var mappedBranchRows = 0;
            var unmappedBranchRows = 0;
            var mappedSubjectRows = 0;
            var unmappedSubjectRows = 0;
            var invalidNationalCodeRows = rows.Count(r => !NationalCodeHelper.IsValid(r.NationalCode));
            var missingLastNameRows = rows.Count(r => string.IsNullOrWhiteSpace(r.LastName));

            foreach (var row in uniqueRows)
            {
                if (!peopleByCode.TryGetValue(row.NationalCode, out var person))
                {
                    person = new Person
                    {
                        NationalCode = row.NationalCode,
                        FirstName = FitPersonName(row.FirstName),
                        LastName = FitPersonName(row.LastName),
                        Mobile = null
                    };
                    _db.People.Add(person);
                    peopleByCode[row.NationalCode] = person;
                    newPeople++;
                }
                else
                {
                    reusedPeople++;
                    if (string.IsNullOrWhiteSpace(person.FirstName)) person.FirstName = FitPersonName(row.FirstName);
                    if (string.IsNullOrWhiteSpace(person.LastName)) person.LastName = FitPersonName(row.LastName);
                }

                var branch = ResolveBranch(row.BranchName, branches, aliases, resolvedBranches, newAliases);
                if (branch == null)
                {
                    branch = fallbackBranch!;
                    unmappedBranchRows++;
                }
                else
                {
                    mappedBranchRows++;
                }

                var subject = ResolveSubject(row.Reason, subjectCatalog, aliases, resolvedSubjects, newAliases);
                if (subject == null)
                {
                    subject = ImportedAppointmentSubject;
                    unmappedSubjectRows++;
                }
                else
                {
                    mappedSubjectRows++;
                }

                var referral = new Referral
                {
                    Person = person,
                    Branch = branch,
                    Subject = subject,
                    Description = BuildDescription(row),
                    ReferralNumber = $"{referralPrefix}{nextReferralNumber++:00000}",
                    ReferralDate = now,
                    Status = ReferralStatus.Referred,
                    CreatedByUserId = referralOwner.Id,
                    CreatedByUserName = referralOwner.FullName,
                    IsVisibleToBranches = false,
                    ImportFingerprint = ComputeFingerprint(row)
                };
                referral.StatusHistories.Add(new ReferralStatusHistory
                {
                    Status = ReferralStatus.Referred,
                    Note = "ثبت‌شده از فایل اکسل نوبت‌ها",
                    ChangedAt = now,
                    ChangedByUserId = referralOwner.Id,
                    ChangedByUserName = referralOwner.FullName,
                    ChangedByRole = AppRoles.EntryController
                });
                _db.Referrals.Add(referral);
            }

            if (newAliases.Count > 0)
                _db.ReferralImportAliases.AddRange(newAliases);

            batch.ImportedRows = uniqueRows.Count;
            batch.NewPeople = newPeople;
            batch.ReusedPeople = reusedPeople;
            batch.MappedBranchRows = mappedBranchRows;
            batch.UnmappedBranchRows = unmappedBranchRows;
            batch.MappedSubjectRows = mappedSubjectRows;
            batch.UnmappedSubjectRows = unmappedSubjectRows;
            batch.InvalidNationalCodeRows = invalidNationalCodeRows;
            batch.MissingLastNameRows = missingLastNameRows;
            _db.ReferralImportBatches.Add(batch);

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return new ReferralExcelImportResult(
                batch.TotalRows,
                batch.ImportedRows,
                batch.SkippedDuplicateRows,
                batch.NewPeople,
                batch.ReusedPeople,
                batch.MappedBranchRows,
                batch.UnmappedBranchRows,
                batch.MappedSubjectRows,
                batch.UnmappedSubjectRows,
                batch.InvalidNationalCodeRows,
                batch.MissingLastNameRows);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            if (await _db.ReferralImportBatches.AnyAsync(b => b.FileSha256 == fileHash))
                throw new DuplicateReferralImportException("این فایل هم‌زمان در درخواست دیگری وارد شده است.");
            throw;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static List<AppointmentRow> ParseWorkbook(byte[] fileBytes)
    {
        try
        {
            using var memory = new MemoryStream(fileBytes, writable: false);
            using var archive = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: false);
            if (archive.Entries.Count > 100 || archive.Entries.Sum(e => e.Length) > MaxExpandedBytes)
                throw new ReferralExcelImportException("ساختار یا حجم بازشدهٔ فایل قابل قبول نیست.");

            var workbook = ReadXml(archive, "xl/workbook.xml");
            var relationships = ReadXml(archive, "xl/_rels/workbook.xml.rels");
            var firstSheet = workbook.Root?.Element(SpreadsheetNs + "sheets")?
                .Elements(SpreadsheetNs + "sheet")
                .FirstOrDefault(s => (string?)s.Attribute("state") != "hidden");
            if (firstSheet == null)
                throw new ReferralExcelImportException("در فایل شیت قابل استفاده‌ای پیدا نشد.");

            var relationshipId = (string?)firstSheet.Attribute(OfficeRelNs + "id");
            var target = relationships.Root?.Elements(PackageRelNs + "Relationship")
                .FirstOrDefault(r => (string?)r.Attribute("Id") == relationshipId)?
                .Attribute("Target")?.Value;
            if (string.IsNullOrWhiteSpace(target))
                throw new ReferralExcelImportException("مسیر شیت اکسل قابل خواندن نیست.");

            target = target.Replace('\\', '/');
            var sheetPath = target.StartsWith('/') ? target.TrimStart('/') : target.StartsWith("xl/") ? target : "xl/" + target;
            if (!sheetPath.StartsWith("xl/worksheets/", StringComparison.Ordinal))
                throw new ReferralExcelImportException("مسیر شیت اکسل معتبر نیست.");

            var sharedStrings = ReadSharedStrings(archive);
            var worksheet = ReadXml(archive, sheetPath);
            var sheetRows = worksheet.Root?.Element(SpreadsheetNs + "sheetData")?
                .Elements(SpreadsheetNs + "row")
                .ToList() ?? new List<XElement>();

            Dictionary<int, string>? headerMap = null;
            var headerRowNumber = 0;
            var columns = new ColumnMap();
            foreach (var row in sheetRows.Take(30))
            {
                var values = ReadRow(row, sharedStrings);
                var candidate = values.ToDictionary(p => p.Key, p => p.Value);
                var found = FindColumns(candidate);
                if (!found.HasRequiredColumns) continue;
                headerMap = candidate;
                columns = found;
                headerRowNumber = ParseRowNumber((string?)row.Attribute("r"));
                break;
            }

            if (headerMap == null)
                throw new ReferralExcelImportException("سرستون‌های لازم در فایل پیدا نشدند. فایل باید ستون‌های نام واحد قضایی، شماره ملی، نام، نام خانوادگی و علت مراجعه را داشته باشد.");

            var result = new List<AppointmentRow>();
            foreach (var row in sheetRows)
            {
                var rowNumber = ParseRowNumber((string?)row.Attribute("r"));
                if (rowNumber <= headerRowNumber) continue;
                var values = ReadRow(row, sharedStrings);
                if (values.Values.All(string.IsNullOrWhiteSpace)) continue;

                string Get(int? column) => column.HasValue && values.TryGetValue(column.Value, out var value) ? value.Trim() : string.Empty;
                result.Add(new AppointmentRow(
                    rowNumber,
                    Get(columns.Branch),
                    Get(columns.AppointmentDate),
                    Get(columns.AppointmentTime),
                    NormalizeDigits(Get(columns.NationalCode)),
                    Get(columns.FirstName),
                    Get(columns.LastName),
                    Get(columns.Duration),
                    Get(columns.CaseInfo),
                    Get(columns.AppointmentType),
                    Get(columns.Reason),
                    Get(columns.RecipientRole),
                    Get(columns.Desk)));
            }

            return result;
        }
        catch (ReferralExcelImportException)
        {
            throw;
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or IOException or ArgumentException)
        {
            throw new ReferralExcelImportException("فایل اکسل قابل خواندن نیست یا ساختار آن معتبر نیست.", ex);
        }
    }

    private static XDocument ReadXml(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName) ?? throw new ReferralExcelImportException("فایل اکسل ناقص است.");
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 10_000_000
        });
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry == null) return new List<string>();

        var document = ReadXml(archive, "xl/sharedStrings.xml");
        return document.Root?.Elements(SpreadsheetNs + "si")
            .Select(si => string.Concat(si.Descendants(SpreadsheetNs + "t").Select(t => t.Value)))
            .ToList() ?? new List<string>();
    }

    private static Dictionary<int, string> ReadRow(XElement row, IReadOnlyList<string> sharedStrings)
    {
        var values = new Dictionary<int, string>();
        foreach (var cell in row.Elements(SpreadsheetNs + "c"))
        {
            var reference = (string?)cell.Attribute("r");
            var column = GetColumnNumber(reference);
            var type = (string?)cell.Attribute("t");
            var value = cell.Element(SpreadsheetNs + "v")?.Value ?? string.Empty;
            if (type == "s" && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                value = index >= 0 && index < sharedStrings.Count ? sharedStrings[index] : string.Empty;
            else if (type == "inlineStr")
                value = string.Concat(cell.Descendants(SpreadsheetNs + "t").Select(t => t.Value));
            else if (type == "b")
                value = value == "1" ? "true" : "false";

            values[column] = value;
        }
        return values;
    }

    private static ColumnMap FindColumns(IReadOnlyDictionary<int, string> headers)
    {
        int? Find(params string[] accepted)
        {
            var acceptedSet = accepted.Select(NormalizeHeader).ToHashSet(StringComparer.Ordinal);
            var matchedHeader = headers.FirstOrDefault(p => acceptedSet.Contains(NormalizeHeader(p.Value)));
            return matchedHeader.Key > 0 ? matchedHeader.Key : null;
        }

        return new ColumnMap
        {
            Branch = Find("نام واحد قضایی", "نام واحد قضائي", "واحد قضایی"),
            NationalCode = Find("شماره ملی", "شماره ملي", "کد ملی", "كد ملي"),
            FirstName = Find("نام"),
            LastName = Find("نام خانوادگی", "نام خانوادگي"),
            Reason = Find("علت مراجعه"),
            AppointmentDate = Find("تاریخ نوبت", "تاريخ نوبت"),
            AppointmentTime = Find("از ساعت", "ساعت نوبت"),
            Duration = Find("مدت به دقیقه", "مدت به دقيقه"),
            CaseInfo = Find("شماره پرونده"),
            AppointmentType = Find("نوع نوبت"),
            RecipientRole = Find("سمت مراجعه شونده", "سمت مراجعه‌شونده"),
            Desk = Find("شماره میز", "شماره ميز")
        };
    }

    private static int GetColumnNumber(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference)) return 0;
        var result = 0;
        foreach (var character in cellReference.TakeWhile(char.IsLetter))
            result = checked(result * 26 + char.ToUpperInvariant(character) - 'A' + 1);
        return result;
    }

    private static int ParseRowNumber(string? rowReference) =>
        int.TryParse(rowReference, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rowNumber) ? rowNumber : 0;

    private static string NormalizeHeader(string value) => NormalizeForMatch(value).Replace(" ", string.Empty, StringComparison.Ordinal);

    private static string NormalizeForMatch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (var original in value.Trim())
        {
            var character = original switch
            {
                'ي' or 'ى' => 'ی',
                'ك' => 'ک',
                'ۀ' => 'ه',
                '\u200c' or '\u200f' or '\u202a' or '\u202b' => ' ',
                '\u0640' => '\0',
                _ => original
            };
            if (character == '\0') continue;

            var digit = CharUnicodeInfo.GetDecimalDigitValue(character);
            if (digit >= 0)
                builder.Append((char)('0' + digit));
            else if (char.IsPunctuation(character))
                builder.Append(' ');
            else
                builder.Append(char.ToLowerInvariant(character));
        }

        return Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
    }

    private static string NormalizeDigits(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            var digit = CharUnicodeInfo.GetDecimalDigitValue(character);
            builder.Append(digit >= 0 ? (char)('0' + digit) : character);
        }
        return builder.ToString().Trim();
    }

    private static Branch? ResolveBranch(
        string sourceValue,
        IReadOnlyCollection<Branch> branches,
        IReadOnlyCollection<ReferralImportAlias> aliases,
        IDictionary<string, Branch?> cache,
        ICollection<ReferralImportAlias> newAliases)
    {
        var key = NormalizeForMatch(sourceValue);
        if (cache.TryGetValue(key, out var cached)) return cached;

        var alias = aliases.FirstOrDefault(a => a.Type == ReferralImportAliasType.Branch && a.NormalizedSourceValue == key);
        var match = alias?.TargetBranchId is int aliasBranchId
            ? branches.FirstOrDefault(b => b.Id == aliasBranchId)
            : null;
        match ??= branches.FirstOrDefault(b => NormalizeForMatch(b.Name) == key);

        if (match == null && key.Contains("اجرای احکام", StringComparison.Ordinal) && key.Contains("شیراز", StringComparison.Ordinal))
        {
            var numberMatch = Regex.Match(key, @"شعبه\s*(\d+)");
            if (numberMatch.Success && int.TryParse(numberMatch.Groups[1].Value, out var branchNumber))
            {
                var candidates = branches.Where(b =>
                {
                    var branchName = NormalizeForMatch(b.Name);
                    var courtName = NormalizeForMatch(b.Court?.Name);
                    var dbNumber = Regex.Match(branchName, @"شعبه\s*(\d+)");
                    return dbNumber.Success &&
                           int.TryParse(dbNumber.Groups[1].Value, out var parsedNumber) && parsedNumber == branchNumber &&
                           courtName.Contains("اجرای احکام", StringComparison.Ordinal) &&
                           courtName.Contains("شیراز", StringComparison.Ordinal);
                }).ToList();
                if (candidates.Count == 1) match = candidates[0];
            }
        }

        cache[key] = match;
        if (match != null && alias == null && key.Length > 0)
        {
            var newAlias = new ReferralImportAlias
            {
                Type = ReferralImportAliasType.Branch,
                SourceValue = sourceValue.Trim()[..Math.Min(sourceValue.Trim().Length, 300)],
                NormalizedSourceValue = key[..Math.Min(key.Length, 300)],
                TargetBranchId = match.Id
            };
            newAliases.Add(newAlias);
        }
        return match;
    }

    private static string? ResolveSubject(
        string sourceValue,
        IReadOnlyDictionary<string, string> subjectCatalog,
        IReadOnlyCollection<ReferralImportAlias> aliases,
        IDictionary<string, string?> cache,
        ICollection<ReferralImportAlias> newAliases)
    {
        var key = NormalizeForMatch(sourceValue);
        if (cache.TryGetValue(key, out var cached)) return cached;

        var alias = aliases.FirstOrDefault(a => a.Type == ReferralImportAliasType.Subject && a.NormalizedSourceValue == key);
        var match = alias?.TargetSubject;
        if (string.IsNullOrWhiteSpace(match))
            subjectCatalog.TryGetValue(key, out match);

        cache[key] = match;
        if (!string.IsNullOrWhiteSpace(match) && alias == null && key.Length > 0)
        {
            var source = sourceValue.Trim();
            newAliases.Add(new ReferralImportAlias
            {
                Type = ReferralImportAliasType.Subject,
                SourceValue = source[..Math.Min(source.Length, 300)],
                NormalizedSourceValue = key[..Math.Min(key.Length, 300)],
                TargetSubject = match
            });
        }
        return match;
    }

    private static string FitPersonName(string? name)
    {
        var value = string.IsNullOrWhiteSpace(name) ? "نامشخص" : name.Trim();
        return value.Length <= 100 ? value : value[..100];
    }

    private static string BuildDescription(AppointmentRow row)
    {
        var lines = new[]
        {
            $"واحد قضایی فایل: {Display(row.BranchName)}",
            $"نام: {Display(row.FirstName)} {Display(row.LastName)}",
            $"تاریخ نوبت: {Display(row.AppointmentDate)}",
            $"ساعت نوبت: {Display(row.AppointmentTime)}",
            $"کد ملی فایل: {Display(row.NationalCode)}",
            $"مدت نوبت (دقیقه): {Display(row.Duration)}",
            $"شماره پرونده: {Display(row.CaseInfo)}",
            $"نوع نوبت: {Display(row.AppointmentType)}",
            $"علت مراجعه: {Display(row.Reason)}",
            $"سمت مراجعه‌شونده: {Display(row.RecipientRole)}",
            $"شماره میز: {Display(row.Desk)}"
        };
        return string.Join(" | ", lines);
    }

    private static string Display(string value) => string.IsNullOrWhiteSpace(value) ? "ثبت نشده" : value.Trim();

    private static string ComputeFingerprint(AppointmentRow row)
    {
        var values = new[]
        {
            row.BranchName, row.AppointmentDate, row.AppointmentTime, row.NationalCode,
            row.FirstName, row.LastName, row.Duration, row.CaseInfo, row.AppointmentType,
            row.Reason, row.RecipientRole, row.Desk
        };
        var canonical = string.Join('\u001f', values.Select(NormalizeForMatch));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private sealed class ColumnMap
    {
        public int? Branch { get; init; }
        public int? NationalCode { get; init; }
        public int? FirstName { get; init; }
        public int? LastName { get; init; }
        public int? Reason { get; init; }
        public int? AppointmentDate { get; init; }
        public int? AppointmentTime { get; init; }
        public int? Duration { get; init; }
        public int? CaseInfo { get; init; }
        public int? AppointmentType { get; init; }
        public int? RecipientRole { get; init; }
        public int? Desk { get; init; }
        public bool HasRequiredColumns => Branch.HasValue && NationalCode.HasValue && FirstName.HasValue && LastName.HasValue && Reason.HasValue;
    }

    private sealed record AppointmentRow(
        int ExcelRow,
        string BranchName,
        string AppointmentDate,
        string AppointmentTime,
        string NationalCode,
        string FirstName,
        string LastName,
        string Duration,
        string CaseInfo,
        string AppointmentType,
        string Reason,
        string RecipientRole,
        string Desk);
}

public sealed record ReferralExcelImportResult(
    int TotalRows,
    int ImportedRows,
    int SkippedDuplicateRows,
    int NewPeople,
    int ReusedPeople,
    int MappedBranchRows,
    int UnmappedBranchRows,
    int MappedSubjectRows,
    int UnmappedSubjectRows,
    int InvalidNationalCodeRows,
    int MissingLastNameRows);

public class ReferralExcelImportException : Exception
{
    public ReferralExcelImportException(string message) : base(message) { }
    public ReferralExcelImportException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class DuplicateReferralImportException : ReferralExcelImportException
{
    public DuplicateReferralImportException(string message) : base(message) { }
}
