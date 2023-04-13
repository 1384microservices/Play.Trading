using System;

namespace Play.Trading.Service.Dtos;

public record StoreItemDto(Guid Id, string Name, string Description, decimal Price, int Quantity);
