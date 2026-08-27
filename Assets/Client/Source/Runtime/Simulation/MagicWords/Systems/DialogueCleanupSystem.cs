using Client.Simulation.Core.Phases;
using Client.Simulation.MagicWords.Components;
using DCFApixels.DragonECS;

namespace Client.Simulation.MagicWords.Systems
{
    /// <summary>Deletes the one-frame components the dialogue demo's simulation owns.</summary>
    /// <remarks>
    /// Four commands and the payload event are each their own entity.
    /// <see cref="RequestAvatarCommand"/> is the exception: it rides on the speaker entity it is
    /// about, so only the component is dropped — deleting the entity would delete the speaker.
    /// </remarks>
    internal sealed class DialogueCleanupSystem : IEcsCleanup, IEcsInject<EcsWorld>
    {
        private EcsWorld _world;
        private EcsPool<RequestAvatarCommand> _avatarRequests;

        public void Cleanup()
        {
            foreach (var entityId in _world.Where(out SingleAspect<LoadDialogueCommand> _))
                _world.DelEntity(entityId);

            foreach (var entityId in _world.Where(out SingleAspect<SkipDialogueCommand> _))
                _world.DelEntity(entityId);

            foreach (var entityId in _world.Where(out SingleAspect<ResetDialogueCommand> _))
                _world.DelEntity(entityId);

            foreach (var entityId in _world.Where(out SingleAspect<ReloadAvatarsCommand> _))
                _world.DelEntity(entityId);

            foreach (var entityId in _world.Where(out SingleAspect<DialoguePayloadEvent> _))
                _world.DelEntity(entityId);

            foreach (var entityId in _world.Where(out SingleAspect<RequestAvatarCommand> _))
                _avatarRequests.Del(entityId);
        }

        public void Inject(EcsWorld obj)
        {
            _world = obj;
            _avatarRequests = obj.GetPool<RequestAvatarCommand>();
        }
    }
}
