namespace SmartStockAI.Core.Contracts.Locations;

public interface ILocationService
{
    Task<IReadOnlyList<LocationDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<LocationDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<LocationDto> CreateAsync(CreateLocationRequest request, CancellationToken cancellationToken = default);
    Task<LocationDto?> UpdateAsync(int id, UpdateLocationRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
