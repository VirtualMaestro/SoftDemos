using Client.Simulation.Shared.Navigation.Systems;
using DCFApixels.DragonECS;

namespace Client.Simulation.Shared.Navigation
{
    public sealed class NavigationModule : IEcsModule
    {
        private readonly DemoCatalog _catalog;

        public NavigationModule(DemoCatalog catalog)
        {
            _catalog = catalog;
        }

        public void Import(EcsPipeline.Builder builder)
        {
            builder.Add(new NavigationSystem(_catalog));
        }
    }
}
