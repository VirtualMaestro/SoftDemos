using Client.Simulation.Core.Phases;
using Client.Adapters.Shared.Services;
using Client.Adapters.Shell.Views;
using Client.Simulation.Core.Navigation.Components;
using DCFApixels.DragonECS;

namespace Client.Adapters.Shell.Systems
{
    /// <summary>Drains the menu and HUD presses into the navigation commands they mean.</summary>
    /// <remarks>
    /// The presses used to be written straight into the world from uGUI's own callback, which put
    /// the command in this frame or the next one depending on where in the frame uGUI raised the
    /// click — the one pair of commands in the project whose frame was undefined rather than merely
    /// late. The views now record the press and this system turns it into a command from inside a
    /// phase body, so the answer is the same every time: the frame this system runs in.
    /// <para>It also removes a hazard the callback carried: a click could arrive between
    /// <c>MenuScreen.SetDemos</c> enabling the buttons and <c>BuildAndInit</c> injecting the world.
    /// A recorded press just waits for the first tick.</para>
    /// <para>The two views come from <see cref="ScreenRegistryService"/> per call rather than from
    /// the constructor: a system holds no engine object (DEU0146), and the registry already scans
    /// the Boot scene it was built in.</para>
    /// </remarks>
    internal sealed class ShellInpSystem : IEcsInput, IEcsInject<EcsWorld>,
        IEcsInject<ScreenRegistryService>
    {
        private EcsWorld _world;
        private ScreenRegistryService _screens;
        private EcsPool<OpenDemoCommand> _openDemo;
        private EcsPool<CloseDemoCommand> _closeDemo;

        public void Input()
        {
            if (_screens.TryGet(out MenuScreen menu) &&
                menu.RequestedDemoIndex != MenuScreen.NoDemoRequested)
            {
                _openDemo.Add(_world.NewEntity()).DemoIndex = menu.RequestedDemoIndex;
                menu.RequestedDemoIndex = MenuScreen.NoDemoRequested;
            }

            if (!_screens.TryGet(out DemoHudView demoHud) || !demoHud.CloseRequested)
                return;

            demoHud.CloseRequested = false;
            _closeDemo.Add(_world.NewEntity());
        }

        public void Inject(EcsWorld obj)
        {
            _world = obj;
            _openDemo = obj.GetPool<OpenDemoCommand>();
            _closeDemo = obj.GetPool<CloseDemoCommand>();
        }

        public void Inject(ScreenRegistryService obj) => _screens = obj;
    }
}
