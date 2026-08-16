using System.Collections.Generic;
using Client.Simulation.Ports;
using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords
{
    public sealed class DialoguePlaybackSystem :
        IEcsRun,
        IEcsInject<EcsWorld>,
        IEcsInject<ITimeService>,
        IEcsInject<ILog>
    {
        private readonly MagicWordsConfig _config;
        private readonly List<int> _pendingLines = new();

        private EcsWorld _world;
        private ITimeService _time;
        private ILog _log;
        private EcsPool<SkipDialogueCommand> _skipCommands;
        private EcsPool<DialogueLineComp> _lines;
        private EcsTagPool<LineVisibleTag> _visibleLines;
        private EcsPool<AvatarLoadComp> _avatarLoads;
        private EcsPool<RequestAvatarCommand> _avatarRequests;

        public DialoguePlaybackSystem(MagicWordsConfig config)
        {
            _config = config;
        }

        public void Run()
        {
            // Drained before the readiness gate so taps made while the payload is still in
            // flight are discarded instead of queueing up and skipping the whole dialogue.
            var skip = _DrainSkipCommands();
            ref var state = ref _world.Get<DialogueStateComp>();

            if (state.State != DialogueLoadState.Ready)
                return;

            ref var playback = ref _world.Get<DialoguePlaybackComp>();

            if (playback.IsComplete)
                return;

            _pendingLines.Clear();
            foreach (var entityId in _world.Where(out PendingLineAspect aspect))
                _pendingLines.Add(entityId);

            if (_pendingLines.Count == 0)
            {
                _Complete(ref playback, skip);
                return;
            }

            if (skip)
            {
                while (_pendingLines.Count > 0)
                    _RevealNext(ref playback);

                _Complete(ref playback, true);
                return;
            }

            if (playback.VisibleLineCount > 0)
            {
                playback.SecondsUntilNextLine -= _time.DeltaSeconds;

                if (playback.SecondsUntilNextLine > 0f)
                {
                    _pendingLines.Clear();
                    return;
                }
            }

            _RevealNext(ref playback);
            playback.SecondsUntilNextLine = _config.LineIntervalSeconds;

            if (_pendingLines.Count == 0)
                _Complete(ref playback, false);
            else
                _pendingLines.Clear();
        }

        private bool _DrainSkipCommands()
        {
            var skip = false;
            foreach (var entityId in _world.Where(out SkipAspect _))
            {
                _skipCommands.Del(entityId);
                skip = true;
            }

            return skip;
        }

        private void _RevealNext(ref DialoguePlaybackComp playback)
        {
            var nextListIndex = 0;
            var nextLineIndex = _lines.Read(_pendingLines[0]).Index;
            for (var listIndex = 1; listIndex < _pendingLines.Count; listIndex++)
            {
                var lineIndex = _lines.Read(_pendingLines[listIndex]).Index;

                if (lineIndex >= nextLineIndex)
                    continue;

                nextListIndex = listIndex;
                nextLineIndex = lineIndex;
            }

            var entityId = _pendingLines[nextListIndex];
            _pendingLines.RemoveAt(nextListIndex);
            _visibleLines.TryAdd(entityId);
            playback.VisibleLineCount++;
            _RequestAvatar(_lines.Read(entityId).Speaker);
        }

        private void _RequestAvatar(entlong speaker)
        {
            if (speaker.TryGetID(out var speakerEntityId) == false ||
                _avatarLoads.Has(speakerEntityId) == false ||
                _avatarLoads.Read(speakerEntityId).State != AvatarLoadState.NotRequested ||
                _avatarRequests.Has(speakerEntityId))
                return;

            _avatarRequests.Add(speakerEntityId);
        }

        private void _Complete(ref DialoguePlaybackComp playback, bool skipped)
        {
            _pendingLines.Clear();
            playback.IsComplete = true;
            _log.Info(
                $"Dialogue playback completed with {playback.VisibleLineCount} revealed line(s); " +
                $"skipped={skipped}.");
        }

        public void Inject(EcsWorld obj)
        {
            _world = obj;
            _skipCommands = obj.GetPool<SkipDialogueCommand>();
            _lines = obj.GetPool<DialogueLineComp>();
            _visibleLines = obj.GetPool<LineVisibleTag>();
            _avatarLoads = obj.GetPool<AvatarLoadComp>();
            _avatarRequests = obj.GetPool<RequestAvatarCommand>();
        }

        public void Inject(ITimeService obj) => _time = obj;
        public void Inject(ILog obj) => _log = obj;

        private sealed class SkipAspect : EcsAspect
        {
            public EcsPool<SkipDialogueCommand> Commands = Inc;
        }

        private sealed class PendingLineAspect : EcsAspect
        {
            public EcsPool<DialogueLineComp> Lines = Inc;
            public EcsTagPool<LineVisibleTag> Visible = Exc;
        }
    }
}
