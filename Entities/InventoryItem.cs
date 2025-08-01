﻿using Common.Library;

namespace Inventory.Service.Entities;

public class InventoryItem : IEntity
{
    public Guid UserId { get; set; }
    public Guid CatalogItemId { get; set; }
    public int Quantity { get; set; }
    public DateTimeOffset AcquiredDate { get; set; }
    public HashSet<Guid> MessageIds { get; set; } = new();
}
