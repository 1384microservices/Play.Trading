using System.Collections.Generic;

namespace Play.Trading.Service.Dtos;

public record StoreDto(IEnumerable<StoreItemDto> Items, decimal UserGil);