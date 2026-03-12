using Returns.Domain.Accounts;
using Returns.Shared.Enums;
using Uni.Domain.Common.Primitives;

namespace Returns.Domain.StorageExtension;

public class StorageExtensionOrder : Entity
{
    private StorageExtensionOrder() {}
    
    private readonly List<StorageExtensionOrderItem> _items = [];
    
    public StorageExtensionOrder(Account account, List<Parcel> parcels)
    {
        Account = account;
        AccountId = account.Id;
        CreatedAt = DateTime.UtcNow;
        parcels.ForEach(x => x.SetConfirmationStatus(ReturnConfirmationStatus.Awaiting));
        _items.AddRange(parcels.Select(x => new StorageExtensionOrderItem(this, x)));
        Status =  ReturnConfirmationStatus.Awaiting;
    }

    public void SetPaid()
    {
        Status = ReturnConfirmationStatus.Approved;
        ModifiedAt = DateTime.UtcNow;
    }
    
    public void Cancel(string? cancellationReason)
    {
        CancellationReason =  cancellationReason;
        Status = ReturnConfirmationStatus.Cancelled;
        ModifiedAt = DateTime.UtcNow;
    }
    
    public Account Account { get; init; }
    
    public Guid AccountId { get; init; }
    
    public ReturnConfirmationStatus  Status { get; private set; }
    
    public string? CancellationReason { get; private set; }
    
    public DateTime CreatedAt { get; private set; }
    
    public DateTime? ModifiedAt { get; private set; }
    
    public IReadOnlyList<StorageExtensionOrderItem> Items => _items;
}
