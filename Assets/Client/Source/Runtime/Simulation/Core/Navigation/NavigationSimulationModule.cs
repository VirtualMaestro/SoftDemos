using Client.Simulation.Core.Navigation.Systems;
using DCFApixels.DragonECS;

namespace Client.Simulation.Core.Navigation
{
    public sealed class NavigationSimulationModule : IEcsModule
    {
        private readonly DemoCatalog _catalog;

        public NavigationSimulationModule(DemoCatalog catalog)
        {
            _catalog = catalog;
        }

        public void Import(EcsPipeline.Builder builder)
        {
            builder.Add(new NavigationSimSystem(_catalog));
            builder.Add(new NavigationClnSystem());
        }
    }
}
