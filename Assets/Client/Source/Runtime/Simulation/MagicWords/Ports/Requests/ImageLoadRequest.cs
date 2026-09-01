namespace Client.Simulation.MagicWords.Ports.Requests
{
    /// <summary>Asks <see cref="IImageLoadService"/> for one speaker's avatar.</summary>
    /// <remarks>
    /// The speaker name travels with the URL so an adapter can keep two requests apart when two
    /// speakers share one address.
    /// </remarks>
    public readonly struct ImageLoadRequest
    {
        /// <summary>Who the image is for. Names the request in logs and in the atlas lookup.</summary>
        public readonly string SpeakerName;

        /// <summary>Where the image comes from. Ignored by an adapter that reads a local atlas.</summary>
        public readonly string Url;

        public ImageLoadRequest(string speakerName, string url)
        {
            SpeakerName = speakerName;
            Url = url;
        }
    }
}
