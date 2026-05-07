using SmartStockAI.Core.Enums;

namespace SmartStockAI.Core.Contracts.Stock;

public interface IStockService
{
    Task<IReadOnlyList<StockDocumentDto>> GetDocumentsAsync(StockDocumentType? type = null, CancellationToken cancellationToken = default);
    Task<StockDocumentDto?> GetDocumentByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<StockDocumentDto> CreateDocumentAsync(CreateStockDocumentRequest request, CancellationToken cancellationToken = default);
    Task<StockDocumentDto?> UpdateDocumentAsync(int id, UpdateStockDocumentRequest request, CancellationToken cancellationToken = default);
    Task<StockDocumentDto?> PostDocumentAsync(int id, CancellationToken cancellationToken = default);
    Task<StockDocumentDto?> CancelDocumentAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockMovementDto>> GetMovementsAsync(int? productId = null, CancellationToken cancellationToken = default);
    Task<StockReservationDto> CreateReservationAsync(CreateStockReservationRequest request, CancellationToken cancellationToken = default);
    Task<StockReservationDto?> ReleaseReservationAsync(int reservationId, CancellationToken cancellationToken = default);
}
