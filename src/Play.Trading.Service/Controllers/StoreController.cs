using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Play.Common;
using Play.Trading.Service.Dtos;
using Play.Trading.Service.Entities;

namespace Play.Trading.Service.Controllers;

[ApiController]
[Route("store")]
public class StoreController : ControllerBase
{
    private readonly IRepository<CatalogItem> _catalogItemRepository;
    private readonly IRepository<ApplicationUser> _applicationUserRepository;
    private readonly IRepository<InventoryItem> _inventoryItemRepository;


    public StoreController(IRepository<CatalogItem> catalogItemRepository, IRepository<ApplicationUser> applicationUserRepository, IRepository<InventoryItem> inventoryItemRepository)
    {
        _catalogItemRepository = catalogItemRepository;
        _applicationUserRepository = applicationUserRepository;
        _inventoryItemRepository = inventoryItemRepository;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<StoreDto>> GetAsync()
    {
        var claims = User.Claims;

        var userId = User.FindFirstValue("sub");

        var catalogItems = await _catalogItemRepository.GetAllAsync();

        var inventoryItems = await _inventoryItemRepository.GetAllAsync(item => item.UserId == Guid.Parse(userId));

        var user = await _applicationUserRepository.GetOneAsync(Guid.Parse(userId));

        var storeItemDtos = catalogItems.Select(item => new StoreItemDto(
            item.Id,
            item.Name,
            item.Description,
            item.Price,
            inventoryItems.FirstOrDefault(ii => ii.CatalogItemId == item.Id)?.Quantity ?? 0));

        var storeDto = new StoreDto(storeItemDtos, user?.Gil ?? 0);

        return Ok(storeDto);
    }
}