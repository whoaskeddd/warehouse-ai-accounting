namespace SmartStockAI.Core.Contracts.Reports;

public interface IReportService
{
    Task<IReadOnlyList<ReportDefinitionDto>> GetDefinitionsAsync(CancellationToken cancellationToken = default);
    Task<ReportResultDto> BuildReportAsync(string reportKey, CancellationToken cancellationToken = default);
    Task<byte[]> ExportReportToExcelAsync(string reportKey, CancellationToken cancellationToken = default);
    Task<ImportedReportSnapshotDto> ImportReportFromExcelAsync(byte[] content, string sourceFileName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ImportedReportSnapshotDto>> GetImportedReportsAsync(CancellationToken cancellationToken = default);
    Task<ReportResultDto?> GetImportedReportAsync(int snapshotId, CancellationToken cancellationToken = default);
}
