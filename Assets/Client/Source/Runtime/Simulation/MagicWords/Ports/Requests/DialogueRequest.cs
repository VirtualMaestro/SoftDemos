namespace Client.Simulation.MagicWords.Ports.Requests
{
    /// <summary>Asks <see cref="IDialogueService"/> for the dialogue payload.</summary>
    /// <remarks>
    /// Empty, and a type all the same: the port's address is its own configuration, so there is
    /// nothing for a caller to say. Written <c>Request(default)</c> at the call site. The day the
    /// dialogue gains a parameter it lands here and no signature moves.
    /// </remarks>
    public readonly struct DialogueRequest
    {
    }
}
