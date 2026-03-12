using Microsoft.EntityFrameworkCore;
using Sellers.Parcels.Domain;
using Sellers.Parcels.Domain.Accounts.Exceptions;
using Sellers.Parcels.Domain.Exceptions;
using Sellers.Parcels.EntityFramework;
using Sellers.Parcels.EntityFramework.Extensions;
using Uni.Cqrs.Abstractions;

namespace Sellers.Parcels.Application.Queries.SearchParcelsForExport;

public class SearchParcelsForExportQueryHandler : IQueryHandler<SearchParcelsForExportQuery, SearchParcelsForExportQueryResponseDto>
{
    private const int InstantExportParcelLimit = 9_999;
    private const int BatchSize = 500;
    
    private readonly SellersParcelsDbContext _dbContext;

    public SearchParcelsForExportQueryHandler(SellersParcelsDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async Task<SearchParcelsForExportQueryResponseDto> HandleAsync(SearchParcelsForExportQuery query, CancellationToken cancellationToken)
    {
        var allFilteredParcelsQuery = _dbContext.Parcels
            .Include(x => x.Marketplace)
            .Include(x => x.Service)
            .Include(x => x.Seller)
            .AsNoTracking()
            .AsSplitQuery()
            .FilterForExcelExport(
                sellerIds: query.SellerIds,
                serviceIds: query.ServiceIds,
                startDate: query.StartDate,
                endDate: query.EndDate,
                trackOrExternalNumbers: query.TrackOrExternalNumbers,
                trackEventTypes: query.TrackEventTypes,
                marketplaceType: query.MarketplaceType,
                accountId: query.AccountId
            );

        if (await allFilteredParcelsQuery.IsInstantExportParcelLimitExceeded(InstantExportParcelLimit, cancellationToken))
            throw new InvalidParcelCount();
            
        DateTime? lastCreatedAt = null;
        Guid? lastId = null;
        List<SearchParcelsForExportItemDto> parcelsToExport = [];
        
        var account = await _dbContext.Accounts
            .AsNoTracking()
            .Include(x => x.Region)
            .Where(x => x.Id == query.AccountId)
            .FirstOrDefaultAsync(cancellationToken)?? throw new AccountNotFoundDomainException(query.AccountId);
        
        while (true)
        {
            // Используем Keyset Pagination по CreatedAt вместо Skip().Take(), чтобы не проходиться по индексу с начала на каждой итерации
            var parcelBatch = allFilteredParcelsQuery
                .Where(x => !lastCreatedAt.HasValue 
                            || x.CreatedAt < lastCreatedAt.Value
                            || (x.CreatedAt == lastCreatedAt.Value && x.Id.CompareTo(lastId.Value) < 0))
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Take(BatchSize);
            
            var loadedParcels =  await ProcessParcelsBatch(parcelBatch, cancellationToken);
            
            if (loadedParcels.Count == 0)
                break;

            parcelsToExport.AddRange(loadedParcels);
            
            if (loadedParcels.Count < BatchSize)
                break;
            
            var lastItem = loadedParcels.Last();
            lastCreatedAt = lastItem.CreatedAt;
            lastId = lastItem.Id;
        }
        
        return new SearchParcelsForExportQueryResponseDto
        {
            Parcels = parcelsToExport.ToArray(),
            Language = account.Region.Language,
        };
    }
    
    private static async Task<List<SearchParcelsForExportItemDto>> ProcessParcelsBatch(IQueryable<Parcel> parcelBatch, CancellationToken cancellationToken)
    {
        return await parcelBatch
            .Select(x => new SearchParcelsForExportItemDto
            {
                Id = x.Id,
                ExternalId = x.ExternalId,
                TrackNumber = x.TrackNumber,
                Service = new SearchParcelsForExportItemDto.ServiceDto
                {
                    Id = x.Service.Id,
                    Name = x.Service.Name
                },
                CreatedAt = x.CreatedAt,
                Weight = x.Weight,
                ActualWeight = x.ActualWeight,
                Dimensions = x.Dimensions,
                ActualDimensions = x.ActualDimensions,
                DeclaredValue = x.DeclaredValue,
                Seller = new SearchParcelsForExportItemDto.SellerDto
                {
                    Id = x.Seller.Id,
                    Name = x.Seller.Name
                },
                ActualTrackEventType = x.ActualTrackEventType
            })
            .ToListAsync(cancellationToken);
    }
}
