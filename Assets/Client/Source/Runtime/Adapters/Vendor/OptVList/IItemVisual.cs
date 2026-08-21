namespace Client.Adapters.Vendor.OptVList
{
    public interface IItemVisual
    {
        // The method is invoked when an item enters the viewport
        void OnShow(IItemData itemData);

        // The method is invoked when an item exits the viewport.
        void OnHide();
    }
}