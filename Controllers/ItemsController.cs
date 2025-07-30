using Common.Library;
using Inventory.Service.Clients;
using Inventory.Service.Dto;
using Inventory.Service.Entities;
using Inventory.Service.Extentions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MassTransit;
using Microsoft.IdentityModel.JsonWebTokens;
using Inventory.Contracts;

namespace Inventory.Service.Controllers;
[Route("[controller]")]
[ApiController]
[Authorize]
public class ItemsController : ControllerBase
{
    private const string AdminRole = "Admin";
    private readonly IRepository<InventoryItem> _inventoryItemsRepository;
    private readonly IPublishEndpoint publishEndpoint;
    //private readonly CatalogClient _catalogClient;
    //public ItemsController(IRepository<InventoryItem> itemsRepository, CatalogClient catalogClient)
    //{
    //    this._itemsRepository = itemsRepository;
    //    this._catalogClient = catalogClient;
    //}
    //private readonly IRepository<InventoryItem> inventoryItemsRepository;
    private readonly IRepository<CatalogItem> catalogItemsRepository;
    public ItemsController(IRepository<InventoryItem> inventoryItemsRepository, IRepository<CatalogItem>
        catalogItemsRepository, IPublishEndpoint publishEndpoint)
    {
        this._inventoryItemsRepository = inventoryItemsRepository;
        this.catalogItemsRepository = catalogItemsRepository;
        this.publishEndpoint = publishEndpoint;
    }



    [HttpGet]
    public async Task<ActionResult<IEnumerable<InventoryItemDto>>> GetAsync(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return BadRequest();
        }
        var currentUserId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (Guid.Parse(currentUserId) != userId)
        {
            if (!User.IsInRole(AdminRole))
            {
                return Forbid();
            }
        }


        var inventoryItemEntities = await _inventoryItemsRepository.GetAllAsync(item => item.UserId == userId);
        var itemIds = inventoryItemEntities.Select(item => item.CatalogItemId);
        var catalogItemEntities = await catalogItemsRepository.GetAllAsync(item => itemIds.Contains(item.Id));
        var inventoryItemDtos = inventoryItemEntities.Select(inventoryItem =>
        {
            var catalogItem = catalogItemEntities.SingleOrDefault(catalogItem => catalogItem.Id ==
                                                                        inventoryItem.CatalogItemId);
            return inventoryItem.AsDto(catalogItem?.Name??"", catalogItem?.Description??"");
        });
        return Ok(inventoryItemDtos);
    }
/*
 * or
 * using
 * Httpclient
 */
    //{
    //    if (userId == Guid.Empty)
    //    {
    //        return BadRequest();
    //    }
    //    var catalogItems = await _catalogClient.GetCatalogItemsAsync();
    //    var inventoryItemEntities = await _inventoryItemsRepository.GetAllAsync(item => item.UserId == userId);
    //    var inventoryItemDtos = inventoryItemEntities.Select(inventoryItem =>
    //    {
    //        var catalogItem = catalogItems.Single(catalogItem => catalogItem.Id == inventoryItem.CatalogItemId);
    //        return inventoryItem.AsDto(catalogItem.Name, catalogItem.Description);
    //    });
    //    return Ok(inventoryItemDtos);
    //}




    [HttpPost]
    public async Task<ActionResult> PostAsync(GrantItemsDto grantItemsDto)
    {
        var inventoryItem = await _inventoryItemsRepository.GetAsync(item => item.UserId == grantItemsDto.UserId &&
                                                                   item.CatalogItemId == grantItemsDto.CatalogItemId);
        if (inventoryItem == null)
        {
            inventoryItem = new InventoryItem
            {
                CatalogItemId = grantItemsDto.CatalogItemId,
                UserId = grantItemsDto.UserId,
                Quantity = grantItemsDto.Quantity,
                AcquiredDate = DateTimeOffset.UtcNow
            };
            await _inventoryItemsRepository.CreateAsync(inventoryItem);
        }
        else
        {
            inventoryItem.Quantity += grantItemsDto.Quantity;
            await _inventoryItemsRepository.UpdateAsync(inventoryItem);
        }
        await publishEndpoint.Publish(new InventoryItemUpdated(
            inventoryItem.UserId,
            inventoryItem.CatalogItemId,
            inventoryItem.Quantity));

        return Ok();
    }
}
