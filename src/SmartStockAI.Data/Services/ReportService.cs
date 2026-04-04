using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using SmartStockAI.Core.Contracts.Reports;
using SmartStockAI.Core.Entities;
using SmartStockAI.Core.Enums;
using SmartStockAI.Data.Context;
using SmartStockAI.Data.Security;

namespace SmartStockAI.Data.Services;

public sealed class ReportService(
    AppDbContext dbContext,
    ICurrentUserAccessor currentUserAccessor,
    IAuditLogWriter auditLogWriter) : IReportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly IReadOnlyList<ReportDefinitionDto> Definitions =
    [
        new()
        {
            Key = "inventory-balance",
            Name = "Остатки и стоимость",
            Description = "Текущие остатки, резерв, минимальный запас и оценка стоимости склада."
        },
        new()
        {
            Key = "turnover-30d",
            Name = "Оборачиваемость за 30 дней",
            Description = "Скорость расхода и примерная оборачиваемость по доступному остатку."
        },
        new()
        {
            Key = "profitability",
            Name = "Прибыльность",
            Description = "Маржа по товару и потенциальная прибыль на текущем остатке."
        }
    ];

    public Task<IReadOnlyList<ReportDefinitionDto>> GetDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureAuthenticated(currentUserAccessor);
        return Task.FromResult(Definitions);
    }

    public async Task<ReportResultDto> BuildReportAsync(string reportKey, CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureAuthenticated(currentUserAccessor);

        return reportKey switch
        {
            "inventory-balance" => await BuildInventoryBalanceReportAsync(cancellationToken),
            "turnover-30d" => await BuildTurnoverReportAsync(cancellationToken),
            "profitability" => await BuildProfitabilityReportAsync(cancellationToken),
            _ => throw new InvalidOperationException($"Unknown report '{reportKey}'.")
        };
    }

    public async Task<byte[]> ExportReportToExcelAsync(string reportKey, CancellationToken cancellationToken = default)
    {
        var report = await BuildReportAsync(reportKey, cancellationToken);
        return ReportExcelSerializer.Export(report);
    }

    public async Task<ImportedReportSnapshotDto> ImportReportFromExcelAsync(byte[] content, string sourceFileName, CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureAuthenticated(currentUserAccessor);

        if (content.Length == 0)
        {
            throw new InvalidOperationException("Excel file is empty.");
        }

        var report = ReportExcelSerializer.Import(content);
        var normalizedFileName = string.IsNullOrWhiteSpace(sourceFileName) ? "report.xlsx" : sourceFileName.Trim();
        var importedByDisplayName = await ResolveCurrentUserDisplayNameAsync(cancellationToken);

        var entity = new ImportedReportSnapshot
        {
            ReportKey = report.ReportKey,
            ReportName = report.ReportName,
            SourceFileName = normalizedFileName,
            GeneratedAtUtc = report.GeneratedAtUtc,
            ImportedAtUtc = DateTime.UtcNow,
            ImportedByUserId = currentUserAccessor.UserId,
            ImportedByDisplayName = importedByDisplayName,
            RowsCount = report.Rows.Count,
            Summary = TrimSummary(report.Summary),
            PayloadJson = JsonSerializer.Serialize(report, JsonOptions)
        };

        dbContext.ImportedReportSnapshots.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogWriter.WriteAsync(
            "Report.Imported",
            nameof(ImportedReportSnapshot),
            entity.Id.ToString(CultureInfo.InvariantCulture),
            $"Report '{entity.ReportName}' imported from '{entity.SourceFileName}'.",
            cancellationToken);

        return MapImportedSnapshot(entity);
    }

    public async Task<IReadOnlyList<ImportedReportSnapshotDto>> GetImportedReportsAsync(CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureAuthenticated(currentUserAccessor);

        var items = await dbContext.ImportedReportSnapshots
            .AsNoTracking()
            .OrderByDescending(x => x.ImportedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return items.Select(MapImportedSnapshot).ToList();
    }

    public async Task<ReportResultDto?> GetImportedReportAsync(int snapshotId, CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureAuthenticated(currentUserAccessor);

        var entity = await dbContext.ImportedReportSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == snapshotId, cancellationToken);

        return entity is null
            ? null
            : JsonSerializer.Deserialize<ReportResultDto>(entity.PayloadJson, JsonOptions);
    }

    private async Task<ReportResultDto> BuildInventoryBalanceReportAsync(CancellationToken cancellationToken)
    {
        var products = await dbContext.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Location)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var columns = new List<ReportColumnDto>
        {
            new() { Key = "sku", Title = "SKU" },
            new() { Key = "name", Title = "Товар" },
            new() { Key = "category", Title = "Категория" },
            new() { Key = "location", Title = "Локация" },
            new() { Key = "currentStock", Title = "Остаток" },
            new() { Key = "reservedStock", Title = "Резерв" },
            new() { Key = "availableStock", Title = "Доступно" },
            new() { Key = "minStock", Title = "Мин. остаток" },
            new() { Key = "purchasePrice", Title = "Цена закупки" },
            new() { Key = "stockCost", Title = "Стоимость остатка" },
            new() { Key = "stockRevenue", Title = "Потенц. выручка" }
        };

        var rows = products.Select(product =>
        {
            var availableStock = product.CurrentStock - product.ReservedStock;
            var stockCost = product.CurrentStock * product.PurchasePrice;
            var stockRevenue = product.CurrentStock * product.SalePrice;

            return new Dictionary<string, string>
            {
                ["sku"] = product.Sku,
                ["name"] = product.Name,
                ["category"] = product.Category?.Name ?? "Без категории",
                ["location"] = product.Location?.Name ?? "Без локации",
                ["currentStock"] = FormatDecimal(product.CurrentStock),
                ["reservedStock"] = FormatDecimal(product.ReservedStock),
                ["availableStock"] = FormatDecimal(availableStock),
                ["minStock"] = FormatDecimal(product.MinStock),
                ["purchasePrice"] = FormatMoney(product.PurchasePrice),
                ["stockCost"] = FormatMoney(stockCost),
                ["stockRevenue"] = FormatMoney(stockRevenue)
            };
        }).ToList();

        var totalCost = products.Sum(x => x.CurrentStock * x.PurchasePrice);
        var criticalCount = products.Count(x => x.CurrentStock - x.ReservedStock <= x.MinStock);

        return new ReportResultDto
        {
            ReportKey = "inventory-balance",
            ReportName = "Остатки и стоимость",
            GeneratedAtUtc = DateTime.UtcNow,
            Summary = $"Позиций: {products.Count}. Критических остатков: {criticalCount}. Стоимость склада: {FormatMoney(totalCost)}.",
            Columns = columns,
            Rows = rows
        };
    }

    private async Task<ReportResultDto> BuildTurnoverReportAsync(CancellationToken cancellationToken)
    {
        var periodStartUtc = DateTime.UtcNow.Date.AddDays(-30);

        var products = await dbContext.Products
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var issueStats = await dbContext.StockMovements
            .AsNoTracking()
            .Where(x => x.Type == StockMovementType.Issue && x.CreatedAt >= periodStartUtc)
            .GroupBy(x => x.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                IssuedQuantity = group.Sum(x => x.Quantity),
                LastIssueAt = group.Max(x => x.CreatedAt)
            })
            .ToDictionaryAsync(x => x.ProductId, cancellationToken);

        const decimal periodDays = 30m;
        var columns = new List<ReportColumnDto>
        {
            new() { Key = "sku", Title = "SKU" },
            new() { Key = "name", Title = "Товар" },
            new() { Key = "availableStock", Title = "Доступный остаток" },
            new() { Key = "issued30Days", Title = "Расход за 30 дней" },
            new() { Key = "avgDailyIssue", Title = "Среднедневной расход" },
            new() { Key = "turnoverDays", Title = "Оборачиваемость, дни" },
            new() { Key = "lastIssueAt", Title = "Последний расход" }
        };

        var rows = products.Select(product =>
        {
            var availableStock = product.CurrentStock - product.ReservedStock;
            issueStats.TryGetValue(product.Id, out var stat);

            var issuedQuantity = stat?.IssuedQuantity ?? 0m;
            var avgDailyIssue = issuedQuantity / periodDays;
            var turnoverDays = avgDailyIssue > 0 ? availableStock / avgDailyIssue : (decimal?)null;

            return new Dictionary<string, string>
            {
                ["sku"] = product.Sku,
                ["name"] = product.Name,
                ["availableStock"] = FormatDecimal(availableStock),
                ["issued30Days"] = FormatDecimal(issuedQuantity),
                ["avgDailyIssue"] = FormatDecimal(avgDailyIssue),
                ["turnoverDays"] = turnoverDays.HasValue ? FormatDecimal(turnoverDays.Value) : "Нет расхода",
                ["lastIssueAt"] = stat is null ? "Нет данных" : FormatDate(stat.LastIssueAt)
            };
        }).ToList();

        var noIssueCount = rows.Count(x => x["lastIssueAt"] == "Нет данных");

        return new ReportResultDto
        {
            ReportKey = "turnover-30d",
            ReportName = "Оборачиваемость за 30 дней",
            GeneratedAtUtc = DateTime.UtcNow,
            Summary = $"Период: последние 30 дней. Без расхода: {noIssueCount} позиций из {products.Count}.",
            Columns = columns,
            Rows = rows
        };
    }

    private async Task<ReportResultDto> BuildProfitabilityReportAsync(CancellationToken cancellationToken)
    {
        var products = await dbContext.Products
            .AsNoTracking()
            .Include(x => x.Supplier)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var columns = new List<ReportColumnDto>
        {
            new() { Key = "sku", Title = "SKU" },
            new() { Key = "name", Title = "Товар" },
            new() { Key = "supplier", Title = "Поставщик" },
            new() { Key = "purchasePrice", Title = "Закупка" },
            new() { Key = "salePrice", Title = "Продажа" },
            new() { Key = "marginPerUnit", Title = "Маржа/ед." },
            new() { Key = "marginPercent", Title = "Маржа, %" },
            new() { Key = "currentStock", Title = "Остаток" },
            new() { Key = "potentialProfit", Title = "Потенц. прибыль" }
        };

        var rows = products.Select(product =>
        {
            var marginPerUnit = product.SalePrice - product.PurchasePrice;
            var marginPercent = product.SalePrice == 0 ? 0 : marginPerUnit / product.SalePrice * 100m;
            var potentialProfit = product.CurrentStock * marginPerUnit;

            return new Dictionary<string, string>
            {
                ["sku"] = product.Sku,
                ["name"] = product.Name,
                ["supplier"] = product.Supplier?.Name ?? "Без поставщика",
                ["purchasePrice"] = FormatMoney(product.PurchasePrice),
                ["salePrice"] = FormatMoney(product.SalePrice),
                ["marginPerUnit"] = FormatMoney(marginPerUnit),
                ["marginPercent"] = FormatDecimal(marginPercent),
                ["currentStock"] = FormatDecimal(product.CurrentStock),
                ["potentialProfit"] = FormatMoney(potentialProfit)
            };
        }).ToList();

        var totalPotentialProfit = products.Sum(x => (x.SalePrice - x.PurchasePrice) * x.CurrentStock);

        return new ReportResultDto
        {
            ReportKey = "profitability",
            ReportName = "Прибыльность",
            GeneratedAtUtc = DateTime.UtcNow,
            Summary = $"Позиций: {products.Count}. Потенциальная прибыль на текущем остатке: {FormatMoney(totalPotentialProfit)}.",
            Columns = columns,
            Rows = rows
        };
    }

    private async Task<string> ResolveCurrentUserDisplayNameAsync(CancellationToken cancellationToken)
    {
        if (!currentUserAccessor.UserId.HasValue)
        {
            return "Unknown";
        }

        return await dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == currentUserAccessor.UserId.Value)
            .Select(x => x.DisplayName)
            .FirstOrDefaultAsync(cancellationToken)
            ?? $"User {currentUserAccessor.UserId.Value.ToString(CultureInfo.InvariantCulture)}";
    }

    private static ImportedReportSnapshotDto MapImportedSnapshot(ImportedReportSnapshot entity)
    {
        return new ImportedReportSnapshotDto
        {
            Id = entity.Id,
            ReportKey = entity.ReportKey,
            ReportName = entity.ReportName,
            SourceFileName = entity.SourceFileName,
            GeneratedAtUtc = entity.GeneratedAtUtc,
            ImportedAtUtc = entity.ImportedAtUtc,
            ImportedByUserId = entity.ImportedByUserId,
            ImportedByDisplayName = entity.ImportedByDisplayName,
            RowsCount = entity.RowsCount,
            Summary = entity.Summary
        };
    }

    private static string FormatDecimal(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string FormatMoney(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatDate(DateTime value) =>
        value.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

    private static string TrimSummary(string value)
    {
        var normalized = value.Trim();
        return normalized.Length <= 512 ? normalized : normalized[..512];
    }

    private static class ReportExcelSerializer
    {
        private static readonly XNamespace SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace PackageNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        private static readonly XNamespace DocumentNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace ContentTypesNs = "http://schemas.openxmlformats.org/package/2006/content-types";

        public static byte[] Export(ReportResultDto report)
        {
            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                WriteEntry(archive, "[Content_Types].xml", BuildContentTypesXml().ToString(SaveOptions.DisableFormatting));
                WriteEntry(archive, "_rels/.rels", BuildRootRelationshipsXml().ToString(SaveOptions.DisableFormatting));
                WriteEntry(archive, "xl/workbook.xml", BuildWorkbookXml().ToString(SaveOptions.DisableFormatting));
                WriteEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationshipsXml().ToString(SaveOptions.DisableFormatting));
                WriteEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(BuildMetaRows(report)).ToString(SaveOptions.DisableFormatting));
                WriteEntry(archive, "xl/worksheets/sheet2.xml", BuildWorksheetXml(BuildDataRows(report)).ToString(SaveOptions.DisableFormatting));
            }

            return stream.ToArray();
        }

        public static ReportResultDto Import(byte[] content)
        {
            using var stream = new MemoryStream(content);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

            var metaSheet = archive.GetEntry("xl/worksheets/sheet1.xml")
                ?? throw new InvalidOperationException("Excel file does not contain metadata sheet.");
            var dataSheet = archive.GetEntry("xl/worksheets/sheet2.xml")
                ?? throw new InvalidOperationException("Excel file does not contain data sheet.");

            var metaRows = ReadWorksheetRows(metaSheet);
            var dataRows = ReadWorksheetRows(dataSheet);

            if (metaRows.Count < 2 || dataRows.Count < 2)
            {
                throw new InvalidOperationException("Excel file has incomplete report structure.");
            }

            var meta = metaRows
                .Skip(1)
                .Where(x => x.Count >= 2)
                .ToDictionary(x => x[0], x => x[1], StringComparer.OrdinalIgnoreCase);

            if (!meta.TryGetValue("ReportKey", out var reportKey) || string.IsNullOrWhiteSpace(reportKey))
            {
                throw new InvalidOperationException("Excel file does not contain report key.");
            }

            if (!meta.TryGetValue("ReportName", out var reportName) || string.IsNullOrWhiteSpace(reportName))
            {
                throw new InvalidOperationException("Excel file does not contain report name.");
            }

            var generatedAtUtc = meta.TryGetValue("GeneratedAtUtc", out var generatedAtRaw)
                && DateTime.TryParse(generatedAtRaw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsedGeneratedAt)
                    ? parsedGeneratedAt
                    : DateTime.UtcNow;

            var titles = dataRows[0];
            var keys = dataRows[1];
            if (titles.Count == 0 || keys.Count == 0 || titles.Count != keys.Count)
            {
                throw new InvalidOperationException("Excel file has invalid header structure.");
            }

            var columns = keys.Select((key, index) => new ReportColumnDto
            {
                Key = key,
                Title = titles.ElementAtOrDefault(index) ?? key
            }).ToList();

            var rows = dataRows
                .Skip(2)
                .Select(values =>
                {
                    var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < keys.Count; i++)
                    {
                        row[keys[i]] = values.ElementAtOrDefault(i) ?? string.Empty;
                    }

                    return row;
                })
                .ToList();

            return new ReportResultDto
            {
                ReportKey = reportKey,
                ReportName = reportName,
                GeneratedAtUtc = generatedAtUtc,
                Summary = meta.GetValueOrDefault("Summary", string.Empty),
                Columns = columns,
                Rows = rows
            };
        }

        private static IEnumerable<IReadOnlyList<string>> BuildMetaRows(ReportResultDto report)
        {
            yield return ["Key", "Value"];
            yield return ["ReportKey", report.ReportKey];
            yield return ["ReportName", report.ReportName];
            yield return ["GeneratedAtUtc", report.GeneratedAtUtc.ToString("O", CultureInfo.InvariantCulture)];
            yield return ["Summary", report.Summary];
        }

        private static IEnumerable<IReadOnlyList<string>> BuildDataRows(ReportResultDto report)
        {
            yield return report.Columns.Select(x => x.Title).ToList();
            yield return report.Columns.Select(x => x.Key).ToList();

            foreach (var row in report.Rows)
            {
                yield return report.Columns
                    .Select(column => row.TryGetValue(column.Key, out var value) ? value : string.Empty)
                    .ToList();
            }
        }

        private static XDocument BuildContentTypesXml()
        {
            return new XDocument(
                new XElement(ContentTypesNs + "Types",
                    new XElement(ContentTypesNs + "Default",
                        new XAttribute("Extension", "rels"),
                        new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                    new XElement(ContentTypesNs + "Default",
                        new XAttribute("Extension", "xml"),
                        new XAttribute("ContentType", "application/xml")),
                    new XElement(ContentTypesNs + "Override",
                        new XAttribute("PartName", "/xl/workbook.xml"),
                        new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                    new XElement(ContentTypesNs + "Override",
                        new XAttribute("PartName", "/xl/worksheets/sheet1.xml"),
                        new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")),
                    new XElement(ContentTypesNs + "Override",
                        new XAttribute("PartName", "/xl/worksheets/sheet2.xml"),
                        new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"))));
        }

        private static XDocument BuildRootRelationshipsXml()
        {
            return new XDocument(
                new XElement(PackageNs + "Relationships",
                    new XElement(PackageNs + "Relationship",
                        new XAttribute("Id", "rId1"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                        new XAttribute("Target", "xl/workbook.xml"))));
        }

        private static XDocument BuildWorkbookXml()
        {
            return new XDocument(
                new XElement(SpreadsheetNs + "workbook",
                    new XAttribute(XNamespace.Xmlns + "r", DocumentNs),
                    new XElement(SpreadsheetNs + "sheets",
                        new XElement(SpreadsheetNs + "sheet",
                            new XAttribute("name", "Meta"),
                            new XAttribute("sheetId", "1"),
                            new XAttribute(DocumentNs + "id", "rId1")),
                        new XElement(SpreadsheetNs + "sheet",
                            new XAttribute("name", "Data"),
                            new XAttribute("sheetId", "2"),
                            new XAttribute(DocumentNs + "id", "rId2")))));
        }

        private static XDocument BuildWorkbookRelationshipsXml()
        {
            return new XDocument(
                new XElement(PackageNs + "Relationships",
                    new XElement(PackageNs + "Relationship",
                        new XAttribute("Id", "rId1"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                        new XAttribute("Target", "worksheets/sheet1.xml")),
                    new XElement(PackageNs + "Relationship",
                        new XAttribute("Id", "rId2"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                        new XAttribute("Target", "worksheets/sheet2.xml"))));
        }

        private static XDocument BuildWorksheetXml(IEnumerable<IReadOnlyList<string>> rows)
        {
            var rowIndex = 1;
            return new XDocument(
                new XElement(SpreadsheetNs + "worksheet",
                    new XElement(SpreadsheetNs + "sheetData",
                        rows.Select(values =>
                        {
                            var currentRowIndex = rowIndex++;
                            return new XElement(SpreadsheetNs + "row",
                                new XAttribute("r", currentRowIndex),
                                values.Select((value, index) => BuildCell(index, currentRowIndex, value)));
                        }))));
        }

        private static XElement BuildCell(int columnIndex, int rowIndex, string value)
        {
            return new XElement(SpreadsheetNs + "c",
                new XAttribute("r", $"{GetColumnName(columnIndex)}{rowIndex}"),
                new XAttribute("t", "inlineStr"),
                new XElement(SpreadsheetNs + "is",
                    new XElement(SpreadsheetNs + "t",
                        new XAttribute(XNamespace.Xml + "space", "preserve"),
                        value ?? string.Empty)));
        }

        private static List<List<string>> ReadWorksheetRows(ZipArchiveEntry entry)
        {
            using var entryStream = entry.Open();
            var document = XDocument.Load(entryStream);

            return document.Root?
                .Element(SpreadsheetNs + "sheetData")?
                .Elements(SpreadsheetNs + "row")
                .Select(row => row.Elements(SpreadsheetNs + "c").Select(ReadCellValue).ToList())
                .ToList()
                ?? [];
        }

        private static string ReadCellValue(XElement cell)
        {
            var inlineValue = cell.Element(SpreadsheetNs + "is")?.Value;
            if (inlineValue is not null)
            {
                return inlineValue;
            }

            return cell.Element(SpreadsheetNs + "v")?.Value ?? string.Empty;
        }

        private static void WriteEntry(ZipArchive archive, string path, string content)
        {
            var entry = archive.CreateEntry(path, CompressionLevel.SmallestSize);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(content);
        }

        private static string GetColumnName(int index)
        {
            const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var builder = new StringBuilder();
            var current = index;

            do
            {
                builder.Insert(0, letters[current % 26]);
                current = current / 26 - 1;
            }
            while (current >= 0);

            return builder.ToString();
        }
    }
}
