using System;

namespace Play.Trading.Service.Dtos;

public record PurchaseDto(
    Guid UserId,
    Guid ItemId,
    decimal? PurchaseTotal,
    int Quantity,
    string CurrentState,
    string Reason,
    DateTimeOffset Received,
    DateTimeOffset LastUpdated
);
