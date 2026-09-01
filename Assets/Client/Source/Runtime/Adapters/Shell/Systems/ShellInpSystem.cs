using Client.Simulation.Core.Phases;
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
    /// </remarks>
    internal sealed class ShellInpSystem : IEcsInput, IEcsInject<EcsWorld>
    {
        private readonly MenuScreen _menu;
        private readonly DemoHudView _demoHud;

        private EcsWorld _world;
        private EcsPool<OpenDemoCommand> _openDemo;
        private EcsPool<CloseDemoCommand> _closeDemo;

        public ShellInpSystem(MenuScreen menu, DemoHudView demoHud)
        {
            _menu = menu;
            _demoHud = demoHud;
        }

        public void Input()
        {
            if (_menu.RequestedDemoIndex != MenuScreen.NoDemoRequested)
            {
                _openDemo.Add(_world.NewEntity()).DemoIndex = _menu.RequestedDemoIndex;
                _menu.RequestedDemoIndex = MenuScreen.NoDemoRequested;
            }

            if (!_demoHud.CloseRequested)
                return;

            _demoHud.CloseRequested = false;
            _closeDemo.Add(_world.NewEntity());
        }

        public void Inject(EcsWorld obj)
        {
            _world = obj;
            _openDemo = obj.GetPool<OpenDemoCommand>();
            _closeDemo = obj.GetPool<CloseDemoCommand>();
        }
    }
}
