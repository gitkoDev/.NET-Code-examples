using Microsoft.Extensions.Options;
using PickUp.Application.Options;
using PickUp.Application.Services.ShipmentTimeValidationService;
using PickUp.Domain.Exceptions;
using Xunit;

namespace PickUp.Domain.Tests;

public class ValidationServiceTests
{
    private readonly DefaultShipmentTimeValidationService _validationService;

    public ValidationServiceTests()
    {
        var pickupOptions = new PickUpOptions { MinShipmentTimeGapInHours = 3.5 };
        var options = Options.Create(pickupOptions);
        
        //Местное время
        var baseTime = new DateTime(2026, 03, 04, 08, 0,0);
        var dateTimeProvider = new TestDateTimeProvider(baseTime);
        _validationService = new DefaultShipmentTimeValidationService(dateTimeProvider, options);
    }
    
    
    [Fact]
    public void Validation_WithLessThanMinTimeGap_ShouldFail()
    {
        //Пикап через 2 часа
        var shipmentLocalTime = new DateTime(2026, 03, 04, 10, 0, 0);
        
        Assert.Throws<RequestFormInvalidTimeDomainException>(() => _validationService.ValidateShipmentTime(shipmentLocalTime));
    }
    
    [Fact]
    public void Validation_ForFutureDate_ShouldPass()
    {
        var shipmentLocalTime = new DateTime(2026, 03, 08, 12, 0, 0);
        
        _validationService.ValidateShipmentTime(shipmentLocalTime);
    }

    [Fact]
    public void Validation_WhenPastMaxTimeForSameDay_ShouldFail()
    {
        //В тот же день >= 19:31
        var shipmentLocalTime = new DateTime(2026, 03, 04, 19, 31,0);
        
        Assert.Throws<RequestFormInvalidTimeDomainException>(() => _validationService.ValidateShipmentTime(shipmentLocalTime));
    }
    
    [Fact]
    public void Validation_OneMinuteBeforeMaxTimeForSameDay_ShouldPass()
    {
        //В тот же день = 19:30:59
        var shipmentLocalTime = new DateTime(2026, 03, 04, 19, 30,59);
        
        _validationService.ValidateShipmentTime(shipmentLocalTime);
    }

    [Fact]
    public void Validation_ExactlyAtMinTimeGap_ShouldPass()
    {
        var shipmentLocalTime = new DateTime(2026, 03, 04, 12, 0,0);
        
        _validationService.ValidateShipmentTime(shipmentLocalTime);
    }
}
