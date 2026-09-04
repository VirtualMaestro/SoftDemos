using DCFApixels.DragonECS;

namespace Client.Adapters.MagicWords.Components.Events
{
    /// <summary>Says the dialogue log content is gone, so the list must drop its rows.</summary>
    /// <remarks>
    /// Added by <c>MagicWordsInpSystem</c> during teardown, read by <c>DialogueLogPreSystem</c>,
    /// which clears its views. One frame: the Magic Words cleanup system deletes it and nothing
    /// else may.
    /// </remarks>
    internal struct DialogueLogResetEvent : IEcsTagComponent { }
}
