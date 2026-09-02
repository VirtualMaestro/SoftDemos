using System.Runtime.CompilerServices;
using Client.Simulation.Core.Phases;
using Client.Adapters.PhoenixFlame.Views;
using Client.Adapters.Shared.Services;
using Client.Simulation.Core.Ports;
using Client.Simulation.PhoenixFlame;
using Client.Simulation.PhoenixFlame.Components;
using DCFApixels.DragonECS;
using UnityEngine;

namespace Client.Adapters.PhoenixFlame.Systems
{
    /// <summary>Mirrors <see cref="FlameStateComp"/> onto the Animator, the phase label and the button.</summary>
    /// <remarks>
    /// The drawing half of the flame stage. It writes nothing to the world and holds no state but
    /// what it last rendered, so the fields below are a repaint cache.
    /// <para>It needs no readiness flag of its own: <see cref="FlameStateComp.IsActive"/> already
    /// says whether the demo is running, and the button stays off until it is — a tap during the
    /// load must not queue an advance.</para>
    /// <para>The screen is resolved through its registry per call and never kept: a system holds no
    /// engine object (DEU0146). Its INSTANCE ID is kept instead, which is the one question the old
    /// reference answered — is this the screen the repaint cache belongs to.</para>
    /// </remarks>
    internal sealed class PhoenixFlamePreSystem : IEcsPresent, IEcsInject<EcsWorld>,
        IEcsInject<ILogService>, IEcsInject<ScreenRegistryService>
    {
        private const string OrangeLabel = "Orange";
        private const string GreenLabel = "Green";
        private const string BlueLabel = "Blue";
        private const string OrangeTrigger = "ToOrange";
        private const string GreenTrigger = "ToGreen";
        private const string BlueTrigger = "ToBlue";

        /// <summary>The blend length on every <c>AnyState</c> transition in <c>PhoenixFlame.controller</c>, in seconds.</summary>
        /// <remarks>
        /// This must agree with <see cref="FlameStateComp.TransitionDurationSeconds"/>, which the
        /// simulation counts down. <see cref="_Snap"/> logs a mismatch.
        /// </remarks>
        private const float AuthoredTransitionSeconds = 1f;

        // Hash once. The Animator takes a state hash, and hashing per frame allocates.
        // Static: the labels and the triggers are constants, so every instance would hash the same.
        private static readonly int[] PhaseHashes = _HashPerPhase(OrangeLabel, GreenLabel, BlueLabel);
        private static readonly int[] PhaseTriggers =
            _HashPerPhase(OrangeTrigger, GreenTrigger, BlueTrigger);

        private EcsWorld _world;
        private ILogService _log;
        private ScreenRegistryService _screens;
        private int _screenInstanceId;
        private FlamePhase _shownPhase;
        private FlamePhase _shownLabelPhase = (FlamePhase)(-1);
        // Nullable so the first write always reaches the button. The scene starts it interactable.
        private bool? _shownInteractable;
        // False until the start phase has been snapped onto the Animator for this run of the demo.
        private bool _hasSnapped;

        /// <summary>Indexes three hashed names by <see cref="FlamePhase"/>.</summary>
        private static int[] _HashPerPhase(string orange, string green, string blue)
        {
            var hashes = new int[FlamePhaseCycle.Count];
            hashes[(int)FlamePhase.Orange] = Animator.StringToHash(orange);
            hashes[(int)FlamePhase.Green] = Animator.StringToHash(green);
            hashes[(int)FlamePhase.Blue] = Animator.StringToHash(blue);
            return hashes;
        }

        public void Present()
        {
            if (!_screens.TryGet(out PhoenixFlameScreen screen))
            {
                // No screen means no Animator to clear: the scene that owned it is gone, and the
                // repaint cache is all that is left to forget.
                _Forget();
                return;
            }

            if (_screenInstanceId != screen.GetInstanceID())
                _ResetFor(screen);

            ref readonly var flame = ref _world.Get<FlameStateComp>();

            if (!flame.IsActive)
            {
                _EndRun(screen);
                return;
            }

            if (_hasSnapped)
                _DriveAnimator(screen, in flame);
            else
                _Snap(screen, in flame);

            _ApplyInteractable(screen, !flame.IsTransitioning);
            _ApplyLabel(screen, flame.CurrentPhase);
        }

        /// <summary>Puts the Animator on the phase the demo starts in, without a blend.</summary>
        private void _Snap(PhoenixFlameScreen screen, in FlameStateComp flame)
        {
            _hasSnapped = true;
            // Play the configured phase. The controller default state does not matter.
            _shownPhase = flame.CurrentPhase;
            // Use Play, not a trigger. The start phase must snap, and a trigger would blend.
            _ResetPhaseTriggers(screen);
            screen.FlameAnimator.Play(PhaseHashes[(int)flame.CurrentPhase], 0, 0f);

            if (Mathf.Approximately(flame.TransitionDurationSeconds, AuthoredTransitionSeconds))
                return;

            _log.Error($"The flame transition is {AuthoredTransitionSeconds}s in " +
                $"PhoenixFlame.controller but {flame.TransitionDurationSeconds}s in the " +
                "simulation; the phase label and the colour will disagree.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _DriveAnimator(PhoenixFlameScreen screen, in FlameStateComp flame)
        {
            if (!flame.IsTransitioning || _shownPhase == flame.NextPhase)
                return;

            _shownPhase = flame.NextPhase;
            // Triggers latch. Clear the other two before you set the one you want.
            _ResetPhaseTriggers(screen);
            screen.FlameAnimator.SetTrigger(PhaseTriggers[(int)flame.NextPhase]);
        }

        private static void _ResetPhaseTriggers(PhoenixFlameScreen screen)
        {
            // Teardown runs while the scene closes, so the Animator can already be destroyed.
            if (screen.FlameAnimator == null)
                return;

            foreach (var trigger in PhaseTriggers)
                screen.FlameAnimator.ResetTrigger(trigger);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _ApplyInteractable(PhoenixFlameScreen screen, bool interactable)
        {
            if (_shownInteractable == interactable)
                return;

            _shownInteractable = interactable;
            screen.AdvanceButton.interactable = interactable;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _ApplyLabel(PhoenixFlameScreen screen, FlamePhase phase)
        {
            if (_shownLabelPhase == phase)
                return;

            _shownLabelPhase = phase;
            screen.PhaseLabel.text = _GetPhaseLabel(phase);
        }

        private static string _GetPhaseLabel(FlamePhase phase)
        {
            if (phase == FlamePhase.Green)
                return GreenLabel;

            if (phase == FlamePhase.Blue)
                return BlueLabel;

            return OrangeLabel;
        }

        /// <summary>The screen exists but the flame does not run: before the start, and after a reset.</summary>
        /// <remarks>
        /// Clearing the triggers here is what makes a reopen start clean. The Animator survives one,
        /// and a latched trigger fires again the moment the graph evaluates.
        /// </remarks>
        private void _EndRun(PhoenixFlameScreen screen)
        {
            // Disable the button for the whole load. A tap must not queue an advance.
            _ApplyInteractable(screen, false);

            if (!_hasSnapped)
                return;

            _ResetPhaseTriggers(screen);
            _hasSnapped = false;
            _shownPhase = FlamePhase.Orange;
            _shownLabelPhase = (FlamePhase)(-1);
        }

        private void _ResetFor(PhoenixFlameScreen screen)
        {
            _screenInstanceId = screen.GetInstanceID();
            _shownPhase = FlamePhase.Orange;
            _shownLabelPhase = (FlamePhase)(-1);
            _shownInteractable = null;
            _hasSnapped = false;
        }

        private void _Forget()
        {
            _screenInstanceId = 0;
            _shownPhase = FlamePhase.Orange;
            _shownLabelPhase = (FlamePhase)(-1);
            _shownInteractable = null;
            _hasSnapped = false;
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILogService obj) => _log = obj;
        public void Inject(ScreenRegistryService obj) => _screens = obj;
    }
}
