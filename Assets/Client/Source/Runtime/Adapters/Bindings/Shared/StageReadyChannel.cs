namespace Game.Adapters.Bindings
{
    /// <summary>Reports whether the live demo has painted its own background yet.</summary>
    public sealed class StageReadyChannel
    {
        /// <summary>True once the live stage system has assigned its background sprite.</summary>
        public bool IsReady { get; private set; }

        public void MarkReady() => IsReady = true;

        public void Clear() => IsReady = false;
    }
}
