using Common.Library;
using Inventory.Contracts;
using Inventory.Service.Entities;
using Inventory.Service.Exceptions;
using MassTransit;

namespace Inventory.Service.Consumers;

public class GrantItemsConsumer : IConsumer<GrantItems>
{
    private readonly IRepository<InventoryItem> inventoryItemsRepository;
    private readonly IRepository<CatalogItem> catalogItemsRepository;
    public GrantItemsConsumer(
        IRepository<InventoryItem> inventoryRepository,
        IRepository<CatalogItem> catalogRepository)
    {
        this.inventoryItemsRepository = inventoryRepository;
        this.catalogItemsRepository = catalogRepository;
    }
    public async Task Consume(ConsumeContext<GrantItems> context)
    {
        var message = context.Message;
        var item = await catalogItemsRepository.GetAsync(message.CatalogItemId);
        if (item == null)
        {
            throw new UnknownItemException(message.CatalogItemId);
        }
        var inventoryItem = await inventoryItemsRepository.GetAsync(
            item => item.UserId == message.UserId && item.CatalogItemId == message.CatalogItemId);
        if (inventoryItem == null)
        {
            inventoryItem = new InventoryItem
            {
                CatalogItemId = message.CatalogItemId,
                UserId = message.UserId,
                Quantity = message.Quantity,
                AcquiredDate = DateTimeOffset.UtcNow
            };
            inventoryItem.MessageIds.Add(context.MessageId.Value);
            await inventoryItemsRepository.CreateAsync(inventoryItem);
        }
        else
        {
            if (inventoryItem.MessageIds.Contains(context.MessageId.Value))
            {
                await context.Publish(new InventoryItemsGranted(message.CorrelationId));
                return;
            }


            inventoryItem.Quantity += message.Quantity;
            inventoryItem.MessageIds.Add(context.MessageId.Value);
            await inventoryItemsRepository.UpdateAsync(inventoryItem);
        }
        var itemsGrantedTask = context.Publish(new InventoryItemsGranted(message.CorrelationId));
        var inventoryUpdatedTask = context.Publish(new InventoryItemUpdated(
            inventoryItem.UserId,
            inventoryItem.CatalogItemId,
            inventoryItem.Quantity));
        await Task.WhenAll(inventoryUpdatedTask, itemsGrantedTask);


        await context.Publish(new InventoryItemsGranted(message.CorrelationId));
    }
}
