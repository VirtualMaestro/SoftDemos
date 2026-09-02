using Client.Adapters.Shell.Systems;
using DCFApixels.DragonECS;

namespace Client.Adapters.Shell
{
    public sealed class ShellModule : IEcsModule
    {
        private readonly DemoEntry[] _demos;

        public ShellModule(DemoEntry[] demos)
        {
            _demos = demos;
        }

        /// <summary>Adds the shell: the menu, the demo HUD and the screen it fades between.</summary>
        /// <remarks>
        /// It used to relay five scene references from the composition root, which was the one
        /// feature whose systems held scene objects. They come from <c>ScreenRegistryService</c>
        /// now — it already scans the Boot scene it is built in — so the only thing that crosses
        /// here is the demo catalog, which is plain data
        /// (adr-an-engine-object-has-one-owner-per-kind, DEU0146). <c>Boot</c> keeps its serialized
        /// fields for <c>SetDemos</c> and its inspector checks.
        /// </remarks>
        public void Import(EcsPipeline.Builder builder)
        {
            builder.Add(new ShellStageInpSystem(_demos));
            builder.Add(new ShellInpSystem());
            builder.Add(new ScreenPreSystem());
        }
    }
}
