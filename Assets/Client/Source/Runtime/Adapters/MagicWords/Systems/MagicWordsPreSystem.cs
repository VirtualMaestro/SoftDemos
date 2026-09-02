using System.Runtime.CompilerServices;
using Client.Simulation.Core.Phases;
using Client.Adapters.MagicWords.Services;
using Client.Adapters.MagicWords.Views;
using Client.Adapters.Shared.Components;
using Client.Adapters.Shared.Services;
using Client.Simulation.MagicWords;
using Client.Simulation.MagicWords.Components;
using DCFApixels.DragonECS;

namespace Client.Adapters.MagicWords.Systems
{
    /// <summary>Mirrors dialogue load state and avatar mode onto the two labels of the demo.</summary>
    /// <remarks>
    /// The drawing half of the dialogue stage. It writes nothing to the world and holds no state
    /// but what it last rendered, so the fields below are a repaint cache: drop them and the screen
    /// still ends up correct, only repainted every frame.
    /// <para>It draws while <see cref="DemoReadyTag"/> exists, which is the Input half saying the
    /// content landed and the screen is covered. That tag is the only thing the two halves share —
    /// the load state machine stays private to the half that owns it.</para>
    /// <para>The screen is resolved through its registry per call and never kept: a system holds no
    /// engine object (DEU0146). Its INSTANCE ID is kept instead, which is the one question the old
    /// reference answered — is this the screen the repaint cache belongs to.</para>
    /// </remarks>
    internal sealed class MagicWordsPreSystem : IEcsPresent, IEcsInject<EcsWorld>,
        IEcsInject<AvatarImageRouterService>, IEcsInject<ScreenRegistryService>
    {
        private const string LocalModeLabel = "Avatars: Local";
        private const string RemoteModeLabel = "Avatars: Remote";
        private const string LoadingStatus = "Loading dialogue…";
        private const string FailedStatus = "Dialogue failed to load. Go back and try again.";

        private EcsWorld _world;
        private AvatarImageRouterService _avatars;
        private ScreenRegistryService _screens;
        private EcsTagPool<DemoReadyTag> _demoReady;
        private int _screenInstanceId;
        private DialogueLoadState _shownDialogueState = (DialogueLoadState)(-1);
        private AvatarMode _shownAvatarMode = (AvatarMode)(-1);

        public void Present()
        {
            if (_demoReady.Count == 0 || !_screens.TryGet(out MagicWordsScreen screen))
            {
                _Forget();
                return;
            }

            if (_screenInstanceId != screen.GetInstanceID())
                _ResetFor(screen);

            _DrawStatusLabel(screen);
            _DrawAvatarModeLabel(screen);
        }

        private void _DrawStatusLabel(MagicWordsScreen screen)
        {
            ref readonly var dialogue = ref _world.Get<DialogueStateComp>();

            if (_shownDialogueState == dialogue.State)
                return;

            _shownDialogueState = dialogue.State;
            var failed = dialogue.State == DialogueLoadState.Failed;
            screen.StatusLabel.gameObject.SetActive(dialogue.State != DialogueLoadState.Ready);
            screen.StatusLabel.text = failed ? FailedStatus : LoadingStatus;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _DrawAvatarModeLabel(MagicWordsScreen screen)
        {
            if (_shownAvatarMode == _avatars.Mode)
                return;

            _shownAvatarMode = _avatars.Mode;
            screen.AvatarModeLabel.text = _shownAvatarMode == AvatarMode.Local
                ? LocalModeLabel
                : RemoteModeLabel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _ResetFor(MagicWordsScreen screen)
        {
            _screenInstanceId = screen.GetInstanceID();
            _shownDialogueState = (DialogueLoadState)(-1);
            _shownAvatarMode = (AvatarMode)(-1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _Forget()
        {
            _screenInstanceId = 0;
            _shownDialogueState = (DialogueLoadState)(-1);
            _shownAvatarMode = (AvatarMode)(-1);
        }

        public void Inject(EcsWorld obj)
        {
            _world = obj;
            _demoReady = obj.GetPool<DemoReadyTag>();
        }

        public void Inject(AvatarImageRouterService obj) => _avatars = obj;
        public void Inject(ScreenRegistryService obj) => _screens = obj;
    }
}
