using System.Collections.Generic;
using System.Text;
using Client.Adapters.MagicWords.Components;
using Client.Adapters.MagicWords.Services;
using Client.Adapters.MagicWords.Views;
using Client.Adapters.Shared.Services;
using Client.Adapters.Vendor.OptVList;
using Client.Simulation.Shared.Ports;
using Client.Simulation.MagicWords;
using Client.Simulation.MagicWords.Components;
using DCFApixels.DragonECS;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Adapters.MagicWords.Systems
{
    /// <summary>
    /// Feeds the dialogue <see cref="VList"/> with one data record per visible line. The list owns
    /// the pooled, virtualized views; this system owns the records only. Content and teardown
    /// arrive through <see cref="DialogueLogChannel"/> rather than direct calls from the stage
    /// system — systems must never hold other systems (see SystemIsolationTests). The stage system
    /// bumps the channel's <c>ResetVersion</c> during teardown; because it runs earlier in the same
    /// LateRun pass, the list is cleared in the same frame the teardown started.
    /// </summary>
    public sealed class DialogueLogSystem : IEcsLateRun, IEcsDestroy,
        IEcsInject<EcsWorld>, IEcsInject<ILogService>, IEcsInject<AvatarImageRouterService>,
        IEcsInject<FadePlayerService>, IEcsInject<DialogueLogChannel>
    {
        private const float FadeSeconds = 0.2f;

        private const string EmojiSizeOpen = "<size=200%>";
        private const string EmojiSizeClose = "</size>";

        private readonly Dictionary<int, DialogueLineItemData> _bindings = new();
        private readonly List<int> _pendingLines = new();
        private readonly HashSet<int> _justAddedItemIds = new();
        private readonly StringBuilder _body = new();

        private EcsWorld _world;
        private ILogService _log;
        private AvatarImageRouterService _avatars;
        private FadePlayerService _tweens;
        private DialogueLogChannel _channel;
        private EcsTagPool<DialogueLineBoundTag> _bound;
        private EcsPool<DialogueLineComp> _lines;
        private EcsPool<DialogueTextComp> _texts;
        private EcsPool<SpeakerComp> _speakers;
        private EcsPool<AvatarComp> _avatarData;
        private EcsPool<AvatarLoadComp> _avatarLoads;
        // The channel nulls Scene before bumping ResetVersion, so teardown needs its own reference.
        private VList _list;
        private int _resetVersion;

        public void LateRun()
        {
            if (_resetVersion != _channel.ResetVersion)
            {
                _resetVersion = _channel.ResetVersion;
                _ClearViews();
            }

            if (_channel.Scene == null)
                return;

            _list = _channel.Scene.LogList;
            _SpawnVisibleLines();
            _ApplyAvatars();
        }

        /// <summary>The list dies with the pipeline even when no teardown bump arrived first.</summary>
        public void Destroy()
        {
            _ClearViews();
        }

        private void _ClearViews()
        {
            foreach (var entityId in _bindings.Keys)
                _bound.TryDel(entityId);

            if (_list != null && _list.IsDisposed == false)
                _list.Clear(0);

            _list = null;
            _bindings.Clear();
            _pendingLines.Clear();
            _justAddedItemIds.Clear();
            _body.Clear();
        }

        private void _SpawnVisibleLines()
        {
            var mwScreen = _channel.Scene;

            foreach (var entityId in _world.Where(out VisibleLineAspect _))
                _pendingLines.Add(entityId);

            // Method-group delegates allocate, but only on frames that reveal a new line.
            _pendingLines.Sort(_CompareLineIndices);
            foreach (var entityId in _pendingLines)
            {
                var speakerId = _lines.Read(entityId).Speaker.TryGetID(out var resolvedSpeakerId)
                    ? resolvedSpeakerId
                    : -1;

                var data = new DialogueLineItemData
                {
                    SpeakerId = speakerId,
                    SpeakerName = speakerId >= 0 && _speakers.Has(speakerId)
                        ? _speakers.Read(speakerId).Name
                        : string.Empty,
                    Side = speakerId >= 0 && _avatarData.Has(speakerId)
                        ? _avatarData.Read(speakerId).Side
                        : AvatarSide.Left,
                    Bubble = _channel.Bubble,
                    Frame = _channel.Frame,
                    Emoji = _channel.Emoji,
                    Body = _BuildBody(_texts.Read(entityId).Segments),
                    Avatar = _channel.Placeholder,
                };

                _list.AddItem(data);
                _bindings.Add(entityId, data);
                _bound.TryAdd(entityId);
                _justAddedItemIds.Add(data.ItemId);
            }

            if (_pendingLines.Count > 0)
            {
                Canvas.ForceUpdateCanvases();
                _list.RefreshViewport();
                _ScrollToNewest(mwScreen.LogScroll);
                _list.ForEachVisual(_FadeInJustAdded);
                _justAddedItemIds.Clear();
            }

            _pendingLines.Clear();
        }

        /// <summary>
        /// VList lays items out from the top of the content and reads <c>content.localPosition.y</c>
        /// as a non-negative scroll offset. Asking the ScrollRect for the bottom while the content
        /// is still shorter than the viewport pushes that offset negative, and every line is then
        /// computed as scrolled past — so a short log stays pinned at the top instead.
        /// </summary>
        private static void _ScrollToNewest(ScrollRect scroll)
        {
            if (scroll.content.rect.height > scroll.viewport.rect.height)
            {
                scroll.verticalNormalizedPosition = 0f;
                return;
            }

            var position = scroll.content.localPosition;
            position.y = 0f;
            scroll.content.localPosition = position;
        }

        private void _FadeInJustAdded(IItemVisual visual, int itemId)
        {
            if (_justAddedItemIds.Contains(itemId) && visual is DialogueLineView view)
                _tweens.FadeIn(view.Group, FadeSeconds);
        }

        private void _ApplyAvatars()
        {
            foreach (var data in _bindings.Values)
            {
                var state = AvatarLoadState.Missing;
                var handleId = 0;

                if (data.SpeakerId >= 0 && _avatarLoads.Has(data.SpeakerId))
                {
                    ref readonly var load = ref _avatarLoads.Read(data.SpeakerId);
                    state = load.State;
                    handleId = load.HandleId;
                }

                if (data.LastState == state && data.LastHandleId == handleId)
                    continue;

                data.LastState = state;
                data.LastHandleId = handleId;

                if (state == AvatarLoadState.Ready && _avatars.TryGetSprite(handleId, out var sprite))
                    data.Avatar = sprite;
                else
                {
                    data.Avatar = _channel.Placeholder;

                    if (state == AvatarLoadState.Ready)
                        _log.Error($"Avatar handle #{handleId} does not resolve for a dialogue line.");
                }

                _list.UpdateItem(data);
            }
        }

        private string _BuildBody(DialogueSegment[] segments)
        {
            _body.Clear();

            if (segments == null)
                return _body.ToString();

            foreach (var segment in segments)
                if (segment.Kind == SegmentKind.Emoji)
                    _body.Append(EmojiSizeOpen)
                        .Append("<sprite name=\"").Append(segment.Value).Append("\">")
                        .Append(EmojiSizeClose);
                else
                    _body.Append("<noparse>").Append(segment.Value).Append("</noparse>");

            return _body.ToString();
        }

        private int _CompareLineIndices(int left, int right) =>
            _lines.Read(left).Index.CompareTo(_lines.Read(right).Index);

        public void Inject(EcsWorld obj)
        {
            _world = obj;
            _bound = obj.GetPool<DialogueLineBoundTag>();
            _lines = obj.GetPool<DialogueLineComp>();
            _texts = obj.GetPool<DialogueTextComp>();
            _speakers = obj.GetPool<SpeakerComp>();
            _avatarData = obj.GetPool<AvatarComp>();
            _avatarLoads = obj.GetPool<AvatarLoadComp>();
        }

        public void Inject(ILogService obj) => _log = obj;
        public void Inject(AvatarImageRouterService obj) => _avatars = obj;
        public void Inject(FadePlayerService obj) => _tweens = obj;
        public void Inject(DialogueLogChannel obj) => _channel = obj;

        private sealed class VisibleLineAspect : EcsAspect
        {
            public EcsTagPool<LineVisibleTag> Visible = Inc;
            public EcsPool<DialogueLineComp> Lines = Inc;
            public EcsPool<DialogueTextComp> Texts = Inc;
            public EcsTagPool<DialogueLineBoundTag> Bound = Exc;
        }
    }
}
