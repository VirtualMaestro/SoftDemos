using System.Collections.Generic;
using Client.Simulation.Ports;
using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords
{
    /// <summary>
    /// Turns the raw payload into entities: one per speaker (with avatar URL or "missing") and
    /// one per dialogue line (with text split into text/emoji segments).
    /// </summary>
    public sealed class DialogueIngestSystem :
        IEcsRun,
        IEcsInject<EcsWorld>,
        IEcsInject<ILog>
    {
        private readonly MagicWordsConfig _config;

        private EcsWorld _world;
        private ILog _log;

        public DialogueIngestSystem(MagicWordsConfig config)
        {
            _config = config;
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
            var speakerPool = _world.GetPool<SpeakerComp>();
            var avatarPool = _world.GetPool<AvatarComp>();
            var avatarLoadPool = _world.GetPool<AvatarLoadComp>();
            var linePool = _world.GetPool<DialogueLineComp>();
            var textPool = _world.GetPool<DialogueTextComp>();
            var lineCount = 0;
            var dialogue = payload.dialogue;

            if (dialogue != null)
            {
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
                        speakerPool.Add(speakerEntityId).Name = line.name;

                        ref var avatarLoad = ref avatarLoadPool.Add(speakerEntityId);

                        if (avatarIndex.TryGet(line.name, out var url, out var side))
                        {
                            ref var avatar = ref avatarPool.Add(speakerEntityId);
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
                    ref var lineComp = ref linePool.Add(lineEntityId);
                    lineComp.Index = lineIndex;
                    lineComp.Speaker = speaker;
                    textPool.Add(lineEntityId).Segments = segments;
                    lineCount++;
                }
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
