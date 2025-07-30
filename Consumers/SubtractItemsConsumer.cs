using Common.Library;
using Inventory.Contracts;
using Inventory.Service.Entities;
using Inventory.Service.Exceptions;
using MassTransit;

namespace Inventory.Service.Consumers;

public class SubtractItemsConsumer : IConsumer<SubtractItems>
{
    private readonly IRepository<InventoryItem> inventoryItemsRepository;
    private readonly IRepository<CatalogItem> catalogItemsRepository;
    public SubtractItemsConsumer(
        IRepository<InventoryItem> inventoryRepository,
        IRepository<CatalogItem> catalogRepository)
    {
        this.inventoryItemsRepository = inventoryRepository;
        this.catalogItemsRepository = catalogRepository;
    }
    public async Task Consume(ConsumeContext<SubtractItems> context)
    {
        var message = context.Message;
        var item = await catalogItemsRepository.GetAsync(message.CatalogItemId);
        if (item == null)
        {
            throw new UnknownItemException(message.CatalogItemId);
        }
        var inventoryItem = await inventoryItemsRepository.GetAsync(
            item => item.UserId == message.UserId && item.CatalogItemId == message.CatalogItemId);
        if (inventoryItem != null)
        {
            if (inventoryItem.MessageIds.Contains(context.MessageId.Value))
            {
                await context.Publish(new InventoryItemsSubtracted(message.CorrelationId));
                return;
            }
            inventoryItem.Quantity -= message.Quantity;
            inventoryItem.MessageIds.Add(context.MessageId.Value);
            await inventoryItemsRepository.UpdateAsync(inventoryItem);
        }
        //await context.Publish(new InventoryItemsSubtracted(message.CorrelationId));
        await context.Publish(new InventoryItemUpdated(
            inventoryItem.UserId,
            inventoryItem.CatalogItemId,
            inventoryItem.Quantity));

    }
}
