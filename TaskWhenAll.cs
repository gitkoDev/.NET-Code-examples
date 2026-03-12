   [Route("{id:guid}")]
    [HttpGet]
    [DomainErrorCodes(DomainErrorCode.ParcelNotFound, DomainErrorCode.AccountNotFound)]
    public async Task<GetParcelBySellerIdsResponse> GetParcel(Guid id, CancellationToken cancellationToken)
    {
        var sellerIds = await _sellersService.GetSellerIdsAsync(cancellationToken);

        var logisticParcelTask = GetLogisticParcel(id, sellerIds, cancellationToken);
        var trackEventsTask = GetLogisticTrackEvents(id, cancellationToken);
        var weightAndDimensionsTask = GetParcelsWeightAndDimensions(id, cancellationToken);
        var previousAndNextOrderDataTask = TryGetPreviousAndNextOrderData(id, cancellationToken);

        await Task.WhenAll(logisticParcelTask, trackEventsTask, weightAndDimensionsTask, previousAndNextOrderDataTask);
        
        var logisticParcel = logisticParcelTask.Result;
        var weightAndDimensions = weightAndDimensionsTask.Result;
        var trackEvents = trackEventsTask.Result;
        var previousAndNextOrderData = previousAndNextOrderDataTask.Result;

        ParcelReturnDeliveryInfo? deliveryInfo = null; 

        if (trackEvents.TrackEvents.Any(x => x.Type >= (int)TrackEventType.ReturnDropOffOnTheWay))
        {
            var returnDeliveryInfoResponse = await _returnsMicroserviceClient.SearchDeliveryInfoAsync(new Returns.Grpc.SearchDeliveryInfoGrpcRequest
            {
                ParcelId = id.ToString()
            }, cancellationToken: cancellationToken);

            if (returnDeliveryInfoResponse?.DeliveryInfo != null)
            {
                deliveryInfo = _mapper.Map<ParcelReturnDeliveryInfo>(returnDeliveryInfoResponse.DeliveryInfo);
            }
        }
        else
        {
            var problematicDeliveryInfoResponse = await _problematicParcelsMicroserviceClient.SearchDeliveryInfoAsync(new ProblematicParcels.Grpc.SearchDeliveryInfoGrpcRequest
            {
                ParcelId = id.ToString()
            }, cancellationToken: cancellationToken);
                
            if (problematicDeliveryInfoResponse?.DeliveryInfo != null)
            {
                deliveryInfo = _mapper.Map<ParcelReturnDeliveryInfo>(problematicDeliveryInfoResponse.DeliveryInfo);
            }
        }

        return new GetParcelBySellerIdsResponse
        {
            Id = Guid.Parse(logisticParcel.Id),
            ExternalId = logisticParcel.ExternalId,
            TrackNumber = logisticParcel.TrackNumber,
            Dimensions = _mapper.Map<Dimensions>(weightAndDimensions.Dimensions),
            Weight = _mapper.Map<Weight>(weightAndDimensions.Weight),
            ActualDimensions = _mapper.Map<Dimensions?>(weightAndDimensions.ActualDimensions),
            ActualWeight = _mapper.Map<Weight?>(weightAndDimensions.ActualWeight),
            DeclaredValue = _mapper.Map<Money>(logisticParcel.DeclaredValue),
            Status = (ParcelStatus?)logisticParcel.Status,
            ServiceId = !string.IsNullOrWhiteSpace(logisticParcel.ServiceId) ? Guid.Parse(logisticParcel.ServiceId) : null,
            ServiceName = logisticParcel.ServiceName,
            Items = _mapper.Map<List<GetParcelBySellerIdsItem>>(logisticParcel.Items),
            TrackEvents = _mapper.Map<List<GetTrackEventsByParcelIdItem>>(trackEvents.TrackEvents),
            WeightLastEvent = logisticParcel.WeightLastEvent != null ? _mapper.Map<GetParcelBySellerIdsEventItem>(logisticParcel.WeightLastEvent) : null,
            ReturnDeliveryInfo = deliveryInfo,
            PreviousOrderId = Guid.TryParse(previousAndNextOrderData.PreviousOrderId, out var previousOrderId) ? previousOrderId : null,
            PreviousOrderTrackNumber = previousAndNextOrderData.PreviousOrderTrackNumber,
            NextOrderId = Guid.TryParse(previousAndNextOrderData.NextOrderId, out var nextOrderId) ? nextOrderId : null,
            NextOrderTrackNumber = previousAndNextOrderData.NextOrderTrackNumber
        };
    }
