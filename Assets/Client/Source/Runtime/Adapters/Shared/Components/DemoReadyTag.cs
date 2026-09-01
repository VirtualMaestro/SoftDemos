using DCFApixels.DragonECS;

namespace Client.Adapters.Shared.Components
{
    /// <summary>Says the live demo has painted itself and the shell may hand the screen over.</summary>
    /// <remarks>
    /// A loaded screen is not a drawn screen. <c>ScreenId.Demo</c> means only that the addressable
    /// scene landed; the stage system starts its atlas and background requests on that frame and
    /// they finish some frames later. Presentation keeps the old screen up until this tag exists.
    /// Added by the stage system of whichever demo is open, once its background sprite is assigned,
    /// and removed by that same system on teardown — which is why it is a latch and not an event.
    /// Presentation asks whether the tag EXISTS; no entity id is stored anywhere.
    /// This is not the stage state <c>Ready</c>: Ace of Shadows spawns its cards over several
    /// frames and Phoenix Flame runs a start handshake, and the screen is already covered by then.
    /// </remarks>
    public struct DemoReadyTag : IEcsTagComponent { }
}
