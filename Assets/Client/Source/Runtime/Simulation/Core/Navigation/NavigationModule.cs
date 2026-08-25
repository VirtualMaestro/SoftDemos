using Client.Simulation.Core.Navigation.Systems;
using DCFApixels.DragonECS;

namespace Client.Simulation.Core.Navigation
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
