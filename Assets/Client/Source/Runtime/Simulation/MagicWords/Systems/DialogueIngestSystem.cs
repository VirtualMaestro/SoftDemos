using System.Collections.Generic;
using Client.Simulation.Core.Ports;
using Client.Simulation.MagicWords.Components;
using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Systems
{
    /// <summary>
    /// Turns the raw payload into entities: one per speaker (with avatar URL or "missing") and
    /// one per dialogue line (with text split into text/emoji segments).
    /// </summary>
    public sealed class DialogueIngestSystem : IEcsRun, IEcsInit,

        IEcsInject<EcsWorld>,
        IEcsInject<ILog>
    {
        private readonly MagicWordsConfig _config;

        private EcsWorld _world;
        private ILog _log;
        private EcsPool<SpeakerComp> _speakerPool;
        private EcsPool<AvatarComp> _avatarPool;
        private EcsPool<AvatarLoadComp> _avatarLoadPool;
        private EcsPool<DialogueLineComp> _linePool;
        private EcsPool<DialogueTextComp> _textPool;

        public DialogueIngestSystem(MagicWordsConfig config)
        {
            _config = config;
        }

        public void Init()
        {
            _speakerPool = _world.GetPool<SpeakerComp>();
            _avatarPool = _world.GetPool<AvatarComp>();
            _avatarLoadPool = _world.GetPool<AvatarLoadComp>();
            _linePool = _world.GetPool<DialogueLineComp>();
            _textPool = _world.GetPool<DialogueTextComp>();
        }

        public void Run()
        {
            ref var state = ref _world.Get<DialogueStateComp>();

            if (state.State != DialogueLoadState.Loading)
                return;

            ref var payloadComp = ref _world.Get<DialoguePayloadComp>();
            var payload = payloadComp.Payload;

            if (payload == null)
                return;

            var avatarIndex = new AvatarIndex(payload.avatars, _log);
            var speakers = new Dictionary<string, entlong>(System.StringComparer.Ordinal);
            var lineCount = 0;
            var dialogue = payload.dialogue;

            if (dialogue != null)
                for (var lineIndex = 0; lineIndex < dialogue.Length; lineIndex++)
                {
                    var line = dialogue[lineIndex];

                    if (line == null)
                    {
                        _log.Warn($"Dialogue entry {lineIndex} is null and was discarded.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(line.name))
                    {
                        _log.Warn($"Dialogue entry {lineIndex} has no speaker name and was discarded.");
                        continue;
                    }

                    if (speakers.TryGetValue(line.name, out var speaker) == false)
                    {
                        var speakerEntityId = _world.NewEntity();
                        _speakerPool.Add(speakerEntityId).Name = line.name;

                        ref var avatarLoad = ref _avatarLoadPool.Add(speakerEntityId);

                        if (avatarIndex.TryGet(line.name, out var url, out var side))
                        {
                            ref var avatar = ref _avatarPool.Add(speakerEntityId);
                            avatar.Url = url;
                            avatar.Side = side;
                            avatarLoad.State = AvatarLoadState.NotRequested;
                        }
                        else
                            avatarLoad.State = AvatarLoadState.Missing;

                        speaker = _world.GetEntityLong(speakerEntityId);
                        speakers.Add(line.name, speaker);
                    }

                    var segments = DialogueTokenizer.Tokenize(
                        line.text, _config.KnownEmojiTokens, out var hasUnknownToken);

                    if (hasUnknownToken)
                        _log.Warn($"Dialogue line {lineIndex} contains an unknown emoji token.");

                    var lineEntityId = _world.NewEntity();
                    ref var lineComp = ref _linePool.Add(lineEntityId);
                    lineComp.Index = lineIndex;
                    lineComp.Speaker = speaker;
                    _textPool.Add(lineEntityId).Segments = segments;
                    lineCount++;
                }

            state.State = DialogueLoadState.Ready;
            state.LineCount = lineCount;
            state.SpeakerCount = speakers.Count;
            payloadComp = default;
        }

        public void Inject(EcsWorld obj) => _world = obj;
        public void Inject(ILog obj) => _log = obj;
    }
}
