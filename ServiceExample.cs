using Microsoft.Extensions.Options;
using PickUp.Application.Options;
using PickUp.Domain.Exceptions;
using PickUp.Domain.Services.DateTimeProvider;
using Uni.Common.Extensions;

namespace PickUp.Application.Services.ShipmentTimeValidationService;

public class DefaultShipmentTimeValidationService : IShipmentTimeValidationService
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly PickUpOptions _options;

    public DefaultShipmentTimeValidationService(
        IDateTimeProvider dateTimeProvider,
        IOptions<PickUpOptions> options)
    {
        _dateTimeProvider = dateTimeProvider;
        _options = options.Value;
    }
    
    public void ValidateShipmentTime(DateTime shipmentLocalTime)
    {
        var currentLocalTime = _dateTimeProvider.UtcNow.ToChinaTimeZone();
        
        //На будущие даты ограничений по времени нет
        if (shipmentLocalTime.Date > currentLocalTime.Date) return;
        
        var timeGap = shipmentLocalTime - currentLocalTime;
        
        if (IsLessThanMinTimeGap(timeGap) || IsPastMaxTimeForSameDay(shipmentLocalTime))
            throw new RequestFormInvalidTimeDomainException(currentLocalTime, shipmentLocalTime);
    }

    private bool IsLessThanMinTimeGap(TimeSpan timeGap) 
        => timeGap.TotalHours < _options.MinShipmentTimeGapInHours;

    private static bool IsPastMaxTimeForSameDay(DateTime shipmentLocalTime)
    {
        var maxTimeForSameDay = new TimeSpan(19, 30, 59);
        
        return shipmentLocalTime.TimeOfDay > maxTimeForSameDay;
    }
}
