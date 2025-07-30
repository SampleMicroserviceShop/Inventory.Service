using Common.Library;

namespace Inventory.Service.Entities;

public class CatalogItem : IEntity
{
    public string Name { get; set; }
    public string Description { get; set; }
}
