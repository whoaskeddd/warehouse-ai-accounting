namespace SmartStockAI.Core.Contracts.Inventory;

public interface IInventoryService
{
    Task<IReadOnlyList<InventorySessionDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<InventorySessionDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<InventorySessionDto> CreateAsync(CreateInventorySessionRequest request, CancellationToken cancellationToken = default);
    Task<InventorySessionDto?> SaveCountAsync(int sessionId, SaveInventoryCountRequest request, CancellationToken cancellationToken = default);
    Task<InventorySessionDto?> CompleteAsync(int sessionId, CancellationToken cancellationToken = default);
}
