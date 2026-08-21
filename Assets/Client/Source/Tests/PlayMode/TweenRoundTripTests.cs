using System.Collections;
using System.Collections.Generic;
using Client.Adapters.AceOfShadows.Services;
using Client.Adapters.AceOfShadows.Systems;
using Client.Adapters.Shared.Services;
using Client.Simulation.Shared.Components;
using Client.Simulation.Shared.Ports;
using DCFApixels.DragonECS;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Adapters.Tests
{
    /// <summary>Proves the command, tween and completion cycle on a throwaway object.</summary>
    /// <remarks>
    /// The test asserts the contract the simulation sees: <see cref="MoveCommand"/> in,
    /// <see cref="MoveCompletedTag"/> out, position reached. That contract holds even if
    /// DOTween is replaced later.
    /// </remarks>
    public sealed class TweenRoundTripTests
    {
        private const int TargetSlot = 1;
        private const float ShortDuration = 0.15f;
        private const float LongDuration = 5f;
        private const float TimeoutSeconds = 5f;

        private EcsWorld _world;
        private EcsPipeline _pipeline;
        private ViewRegistryService _registry;
        private StackSlotLayoutService _layout;
        private GameObject _view;

        [SetUp]
        public void SetUp()
        {
            _view = new GameObject("TweenRoundTripView");
            _registry = new ViewRegistryService();
            // A portrait recalculation puts slot 1 away from the origin, so "the view moved"
            // and "the view arrived" both assert against a non-zero target.
            _layout = new StackSlotLayoutService();
            _layout.Recalculate(1080, 1920, 5f);

            _world = new EcsWorld();
            var player = new TweenPlayerService(_world, _registry);
            _pipeline = EcsPipeline.New()
                .Inject(_world)
                .Inject<ILogService>(new UnityLogService("Test.Tween"))
                .Inject<ViewRegistryService>(_registry)
                .Inject(_layout)
                .Inject(player)
                .Add(new TweenPlaybackSystem())
                .BuildAndInit();
        }

        [TearDown]
        public void TearDown()
        {
            _pipeline?.Destroy();
            _pipeline = null;

            _world?.Destroy();
            _world = null;

            if (_view != null)
                Object.DestroyImmediate(_view);

            _view = null;
        }

        [UnityTest]
        public IEnumerator MoveCommand_BecomesMoveCompleted_AndTheViewReachesTheSlot()
        {
            var entityId = _CreateMovingEntity(ShortDuration);
            var completed = _world.GetPool<MoveCompletedTag>();
            var commands = _world.GetPool<MoveCommand>();

            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (completed.Has(entityId) == false)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    $"MoveCompletedTag never appeared within {TimeoutSeconds}s. " +
                    $"Position: {_view.transform.position}, target: {_layout.SlotPosition(TargetSlot, 0)}.");

                yield return null;
                _pipeline.LateRun();
            }

            Assert.That(commands.Has(entityId), Is.False,
                "The adapter must remove MoveCommand when it reports MoveCompletedTag.");
            Assert.That(_view.transform.position,
                Is.EqualTo(_layout.SlotPosition(TargetSlot, 0)).Using(new Vector3Comparer(0.001f)),
                $"The view should have reached slot {TargetSlot}.");
        }

        /// <summary>
        /// A tween outliving its world would call back into a destroyed pipeline. The system kills
        /// every registered view's tweens in <c>IEcsDestroy</c>; the observable proof is that the
        /// transform stops moving the moment the world goes away.
        /// </summary>
        [UnityTest]
        public IEnumerator DestroyingTheWorldMidTween_StopsTheTween()
        {
            _CreateMovingEntity(LongDuration);

            // Let the tween actually get going, otherwise "it stopped" proves nothing.
            for (var frame = 0; frame < 3; frame++)
            {
                yield return null;
                _pipeline.LateRun();
            }

            var movedTo = _view.transform.position;
            Assert.That(movedTo, Is.Not.EqualTo(Vector3.zero).Using(new Vector3Comparer(0.0001f)),
                "The tween should have moved the view before the world is destroyed.");

            _pipeline.Destroy();
            _pipeline = null;
            _world.Destroy();
            _world = null;

            var positionAtDestroy = _view.transform.position;

            for (var frame = 0; frame < 3; frame++)
                yield return null;

            Assert.That(_view.transform.position,
                Is.EqualTo(positionAtDestroy).Using(new Vector3Comparer(0.0001f)),
                "The tween kept running after the world was destroyed — IEcsDestroy did not kill it.");
        }

        private int _CreateMovingEntity(float duration)
        {
            var handleId = _registry.Register(_view.transform);

            var entityId = _world.NewEntity();
            _world.GetPool<ViewHandleComp>().Add(entityId).Id = handleId;

            ref var command = ref _world.GetPool<MoveCommand>().Add(entityId);
            command.TargetSlot = TargetSlot;
            command.Duration = duration;

            _pipeline.LateRun(); // picks the command up and starts the tween
            return entityId;
        }

        /// <summary>Tolerant Vector3 equality — floating point plus easing never lands exactly.</summary>
        private sealed class Vector3Comparer : IEqualityComparer<Vector3>
        {
            private readonly float _tolerance;

            public Vector3Comparer(float tolerance) => _tolerance = tolerance;

            public bool Equals(Vector3 a, Vector3 b) => Vector3.Distance(a, b) <= _tolerance;

            public int GetHashCode(Vector3 value) => 0;
        }
    }
}
