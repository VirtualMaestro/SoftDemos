using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Client.Simulation.Core.Phases;
using Client.Adapters.MagicWords.Components;
using Client.Adapters.MagicWords.Components.Events;
using Client.Adapters.MagicWords.Services;
using Client.Adapters.MagicWords.Views;
using Client.Adapters.Shared.Components;
using Client.Adapters.Shared.Services;
using Client.Adapters.Vendor.OptVList;
using Client.Simulation.Core.Ports;
using Client.Simulation.MagicWords;
using Client.Simulation.MagicWords.Components;
using DCFApixels.DragonECS;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Adapters.MagicWords.Systems
{
    /// <summary>
    /// Feeds the dialogue <see cref="VList"/> with one data record per visible line. The list owns
    /// the pooled, virtualized views; this system owns the records only. Teardown arrives through
    /// <c>DialogueLogResetEvent</c> rather than a direct call from the stage system — systems must
    /// never hold other systems (see SystemIsolationTests).
    /// </summary>
    /// <remarks>
    /// It holds no engine object. The four art pieces a line draws with arrive as
    /// <see cref="DialogueLogArtComp"/> — 4 <c>int</c>s the Input half wrote — and are resolved
    /// through <see cref="AddressablesAssetService"/> on the call that builds the line; the screen
    /// and its list come from <see cref="ScreenRegistryService"/> per call. Readiness is
    /// <c>DemoReadyTag</c>, the same fact its sibling <c>MagicWordsPreSystem</c> gates on, rather
    /// than a latch of its own (adr-an-engine-object-has-one-owner-per-kind, DEU0146).
    /// <para>It owns nothing either. Which row a line is bound to, and the avatar state that row
    /// was drawn with, live in <see cref="DialogueLineViewComp"/> on the line's own entity — one
    /// component where a dictionary and a mirroring tag used to record the same fact twice. What
    /// stays here is per-call scratch, cleared on the call that fills it
    /// (adr-data-placement-is-decided-on-three-axes rules 4 and 10). There is no
    /// <c>IEcsDestroy</c>: the list belongs to the scene and dies with it.</para>
    /// </remarks>
    internal sealed class DialogueLogPreSystem : IEcsPresent,
        IEcsInject<EcsWorld>, IEcsInject<ILogService>, IEcsInject<AvatarImageRouterService>,
        IEcsInject<FadePlayerService>, IEcsInject<AddressablesAssetService>,
        IEcsInject<ScreenRegistryService>
    {
        private const float FadeSeconds = 0.2f;

        private const string EmojiSizeOpen = "<size=200%>";
        private const string EmojiSizeClose = "</size>";

        private readonly List<int> _pendingLines = new();
        private readonly HashSet<int> _justAddedItemIds = new();
        private readonly StringBuilder _body = new();

        private EcsWorld _world;
        private ILogService _log;
        private AvatarImageRouterService _avatars;
        private FadePlayerService _tweens;
        private AddressablesAssetService _assets;
        private ScreenRegistryService _screens;
        private EcsPool<DialogueLineViewComp> _rows;
        private EcsPool<DialogueLineComp> _lines;
        private EcsPool<DialogueTextComp> _texts;
        private EcsPool<SpeakerComp> _speakers;
        private EcsPool<AvatarComp> _avatarData;
        private EcsPool<AvatarLoadComp> _avatarLoads;
        private EcsTagPool<DialogueLogResetEvent> _logReset;
        private EcsTagPool<DemoReadyTag> _demoReady;

        public void Present()
        {
            if (_logReset.Count > 0)
                _ClearRows();

            // The same readiness fact the sibling MagicWordsPreSystem gates on. The Input half
            // deletes the tag before it releases anything, so this frame's Present is already out
            // by the time an art id stops resolving.
            if (_demoReady.Count == 0)
                return;

            if (!_screens.TryGet(out MagicWordsScreen screen))
                return;

            _SpawnVisibleLines(screen);
            _ApplyAvatars(screen);
        }

        /// <summary>Unbinds every line and empties the list. The reset event is the only caller.</summary>
        private void _ClearRows()
        {
            foreach (var entityId in _world.Where(out SingleAspect<DialogueLineViewComp> _))
                _pendingLines.Add(entityId);

            foreach (var entityId in _pendingLines)
                _rows.Del(entityId);

            _pendingLines.Clear();

            if (_screens.TryGet(out MagicWordsScreen screen))
            {
                var list = screen.LogList;

                if (list != null && !list.IsDisposed)
                    list.Clear(0);
            }

            _justAddedItemIds.Clear();
            _body.Clear();
        }

        private void _SpawnVisibleLines(MagicWordsScreen screen)
        {
            var list = screen.LogList;

            foreach (var entityId in _world.Where(out VisibleLineAspect _))
                _pendingLines.Add(entityId);

            // Method-group delegates allocate, but only on frames that reveal a new line.
            _pendingLines.Sort(_CompareLineIndices);

            foreach (var entityId in _pendingLines)
            {
                var data = _BuildItemData(entityId, _Sprite(_world.Get<DialogueLogArtComp>().Placeholder));

                list.AddItem(data);
                ref var row = ref _rows.Add(entityId);
                row.ItemId = data.ItemId;
                // The re-entry values of the poll cache: the first _ApplyAvatars pass always draws.
                row.LastState = (AvatarLoadState)(-1);
                row.LastRequestId = -1;
                _justAddedItemIds.Add(data.ItemId);
            }

            if (_pendingLines.Count > 0)
            {
                Canvas.ForceUpdateCanvases();
                list.RefreshViewport();
                _ScrollToNewest(screen.LogScroll);
                list.ForEachVisual(_FadeInJustAdded);
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

        /// <summary>Redraws the rows whose speaker's avatar poll moved since the last pass.</summary>
        /// <remarks>
        /// The record the list holds is rebuilt rather than mutated in place, because the row's own
        /// state is the component now and the record is what the view draws. Only a row whose poll
        /// actually moved is rebuilt, which is the comparison the record used to carry itself.
        /// </remarks>
        private void _ApplyAvatars(MagicWordsScreen screen)
        {
            var list = screen.LogList;
            var placeholder = _Sprite(_world.Get<DialogueLogArtComp>().Placeholder);

            foreach (var entityId in _world.Where(out SingleAspect<DialogueLineViewComp> _))
            {
                var speakerId = _SpeakerOf(entityId);
                var state = AvatarLoadState.Missing;
                var requestId = 0;

                if (speakerId >= 0 && _avatarLoads.Has(speakerId))
                {
                    ref readonly var load = ref _avatarLoads.Read(speakerId);
                    state = load.State;
                    requestId = load.RequestId;
                }

                ref var row = ref _rows.Get(entityId);

                if (row.LastState == state && row.LastRequestId == requestId)
                    continue;

                row.LastState = state;
                row.LastRequestId = requestId;
                var itemId = row.ItemId;

                Sprite avatar;

                if (state == AvatarLoadState.Ready && _avatars.TryGetSprite(requestId, out var sprite))
                {
                    avatar = sprite;
                }
                else
                {
                    avatar = placeholder;

                    if (state == AvatarLoadState.Ready)
                        _log.Error($"Avatar request #{requestId} does not resolve for a dialogue line.");
                }

                var data = _BuildItemData(entityId, avatar);
                data.ItemId = itemId;
                list.UpdateItem(data);
            }
        }

        /// <summary>Everything one row draws, read out of the world on the call that needs it.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private DialogueLineItemData _BuildItemData(int entityId, Sprite avatar)
        {
            ref readonly var art = ref _world.Get<DialogueLogArtComp>();
            var speakerId = _SpeakerOf(entityId);

            return new DialogueLineItemData
            {
                SpeakerId = speakerId,
                SpeakerName = speakerId >= 0 && _speakers.Has(speakerId)
                    ? _speakers.Read(speakerId).Name
                    : string.Empty,
                Side = speakerId >= 0 && _avatarData.Has(speakerId)
                    ? _avatarData.Read(speakerId).Side
                    : AvatarSide.Left,
                Bubble = _Sprite(art.Bubble),
                Frame = _Sprite(art.Frame),
                Emoji = _assets.TryGetAsset(art.Emoji, out var emoji)
                    ? emoji as TMP_SpriteAsset
                    : null,
                Body = _BuildBody(_texts.Read(entityId).Segments),
                Avatar = avatar,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int _SpeakerOf(int lineEntityId) =>
            _lines.Read(lineEntityId).Speaker.TryGetID(out var speakerId) ? speakerId : -1;

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

        /// <summary>The sprite an art id names, resolved through its owner and kept by nobody.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Sprite _Sprite(int requestId) =>
            _assets.TryGetAsset(requestId, out var asset) ? asset as Sprite : null;

        private int _CompareLineIndices(int left, int right) =>
            _lines.Read(left).Index.CompareTo(_lines.Read(right).Index);

        public void Inject(EcsWorld obj)
        {
            _world = obj;
            _rows = obj.GetPool<DialogueLineViewComp>();
            _lines = obj.GetPool<DialogueLineComp>();
            _texts = obj.GetPool<DialogueTextComp>();
            _speakers = obj.GetPool<SpeakerComp>();
            _avatarData = obj.GetPool<AvatarComp>();
            _avatarLoads = obj.GetPool<AvatarLoadComp>();
            _logReset = obj.GetPool<DialogueLogResetEvent>();
            _demoReady = obj.GetPool<DemoReadyTag>();
        }

        public void Inject(ILogService obj) => _log = obj;
        public void Inject(AvatarImageRouterService obj) => _avatars = obj;
        public void Inject(FadePlayerService obj) => _tweens = obj;
        public void Inject(AddressablesAssetService obj) => _assets = obj;
        public void Inject(ScreenRegistryService obj) => _screens = obj;

        private sealed class VisibleLineAspect : EcsAspect
        {
            public readonly EcsTagPool<LineVisibleTag> Visible = Inc;
            public readonly EcsPool<DialogueLineComp> Lines = Inc;
            public readonly EcsPool<DialogueTextComp> Texts = Inc;
            public readonly EcsPool<DialogueLineViewComp> Rows = Exc;
        }
    }
}
