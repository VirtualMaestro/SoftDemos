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
    /// what it last rendered, so the fields below are a repaint cache, not a channel.
    /// <para>It needs no readiness flag of its own: <see cref="FlameStateComp.IsActive"/> already
    /// says whether the demo is running, and the button stays off until it is — a tap during the
    /// load must not queue an advance.</para>
    /// </remarks>
    internal sealed class PhoenixFlameViewSystem : IEcsPresent, IEcsInject<EcsWorld>,
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
        private PhoenixFlameScreen _screen;
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
            if (!_screens.TryGet(out PhoenixFlameScreen current))
            {
                _ResetPhaseTriggers();
                _ResetFor(null);
                return;
            }

            if (_screen != current)
                _ResetFor(current);

            ref readonly var flame = ref _world.Get<FlameStateComp>();

            if (!flame.IsActive)
            {
                _EndRun();
                return;
            }

            if (_hasSnapped)
                _DriveAnimator(in flame);
            else
                _Snap(in flame);

            _ApplyInteractable(!flame.IsTransitioning);
            _ApplyLabel(flame.CurrentPhase);
        }

        /// <summary>Puts the Animator on the phase the demo starts in, without a blend.</summary>
        private void _Snap(in FlameStateComp flame)
        {
            _hasSnapped = true;
            // Play the configured phase. The controller default state does not matter.
            _shownPhase = flame.CurrentPhase;
            // Use Play, not a trigger. The start phase must snap, and a trigger would blend.
            _ResetPhaseTriggers();
            _screen.FlameAnimator.Play(PhaseHashes[(int)flame.CurrentPhase], 0, 0f);

            if (Mathf.Approximately(flame.TransitionDurationSeconds, AuthoredTransitionSeconds))
                return;

            _log.Error($"The flame transition is {AuthoredTransitionSeconds}s in " +
                $"PhoenixFlame.controller but {flame.TransitionDurationSeconds}s in the " +
                "simulation; the phase label and the colour will disagree.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _DriveAnimator(in FlameStateComp flame)
        {
            if (!flame.IsTransitioning || _shownPhase == flame.NextPhase)
                return;

            _shownPhase = flame.NextPhase;
            // Triggers latch. Clear the other two before you set the one you want.
            _ResetPhaseTriggers();
            _screen.FlameAnimator.SetTrigger(PhaseTriggers[(int)flame.NextPhase]);
        }

        private void _ResetPhaseTriggers()
        {
            // `?.` skips Unity's null overload. Teardown runs while the scene closes.
            if (_screen == null || _screen.FlameAnimator == null)
                return;

            foreach (var trigger in PhaseTriggers)
                _screen.FlameAnimator.ResetTrigger(trigger);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _ApplyInteractable(bool interactable)
        {
            if (_shownInteractable == interactable)
                return;

            _shownInteractable = interactable;
            _screen.AdvanceButton.interactable = interactable;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _ApplyLabel(FlamePhase phase)
        {
            if (_shownLabelPhase == phase)
                return;

            _shownLabelPhase = phase;
            _screen.PhaseLabel.text = _GetPhaseLabel(phase);
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
        private void _EndRun()
        {
            // Disable the button for the whole load. A tap must not queue an advance.
            _ApplyInteractable(false);

            if (!_hasSnapped)
                return;

            _ResetPhaseTriggers();
            _hasSnapped = false;
            _shownPhase = FlamePhase.Orange;
            _shownLabelPhase = (FlamePhase)(-1);
        }

        private void _ResetFor(PhoenixFlameScreen screen)
        {
            _screen = screen;
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
