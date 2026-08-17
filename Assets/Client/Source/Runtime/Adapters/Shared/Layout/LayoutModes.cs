namespace Client.Adapters.Layout
{
    public static class LayoutModes
    {
        public static LayoutMode FromAspect(float aspect) =>
            aspect < 1f ? LayoutMode.Portrait : LayoutMode.Landscape;
    }
}
