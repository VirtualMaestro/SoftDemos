using Client.Adapters.Vendor;
﻿namespace Client.Adapters.Vendor
{
    public interface IItemData
    {
        // id which was assigned when the item was added to the list. 
        int ItemId { get; set; }
    }
}