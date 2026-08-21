using Client.Simulation.Menu.Systems;
using DCFApixels.DragonECS;

namespace Client.Simulation.Menu
{
    public sealed class MenuModule : IEcsModule
    {
        private readonly DemoCatalog _catalog;

        public MenuModule(DemoCatalog catalog)
        {
            _catalog = catalog;
        }

        public void Import(EcsPipeline.Builder builder)
        {
            builder.Add(new NavigationSystem(_catalog));
        }
    }
}
