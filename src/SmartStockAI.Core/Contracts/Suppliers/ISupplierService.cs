namespace SmartStockAI.Core.Contracts.Suppliers;

public interface ISupplierService
{
    Task<IReadOnlyList<SupplierDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<SupplierDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<SupplierDto> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken = default);
    Task<SupplierDto?> UpdateAsync(int id, UpdateSupplierRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
