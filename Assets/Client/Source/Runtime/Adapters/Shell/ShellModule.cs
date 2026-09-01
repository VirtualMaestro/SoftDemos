using Client.Adapters.Shell.Systems;
using Client.Adapters.Shell.Views;
using DCFApixels.DragonECS;
using UnityEngine;

namespace Client.Adapters.Shell
{
    public sealed class ShellModule : IEcsModule
    {
        private readonly MenuScreen _menu;
        private readonly DemoHudView _demoHud;
        private readonly GameObject _loadingIndicator;
        private readonly ShellSkinView _skin;
        private readonly DemoEntry[] _demos;

        public ShellModule(
            MenuScreen menu,
            DemoHudView demoHud,
            GameObject loadingIndicator,
            ShellSkinView skin,
            DemoEntry[] demos)
        {
            _menu = menu;
            _demoHud = demoHud;
            _loadingIndicator = loadingIndicator;
            _skin = skin;
            _demos = demos;
        }

        /// <summary>Adds the shell: the menu, the demo HUD and the screen it fades between.</summary>
        /// <remarks>
        /// Five scene references against nought or one for the demo features, and that is the shape
        /// of the shell rather than a smell: every one of them stays a serialized field on the
        /// composition root, which needs them for <c>SetDemos</c> and for its inspector checks, so
        /// this constructor relays what the root already holds instead of taking ownership of it.
        /// Construction happens inside <c>Import</c>, which runs during <c>BuildAndInit</c>, so the
        /// scene objects the presentation system reads in its own constructor are already live.
        /// </remarks>
        public void Import(EcsPipeline.Builder builder)
        {
            builder.Add(new ShellStageInpSystem(_skin, _demos));
            builder.Add(new ShellInpSystem(_menu, _demoHud));
            builder.Add(new ScreenPreSystem(_menu, _demoHud, _loadingIndicator, _skin));
        }
    }
}
