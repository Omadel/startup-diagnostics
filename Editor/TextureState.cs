namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     A texture that a scene pulls in, with its largest side in pixels once imported for Android and for iOS.
    /// </summary>
    internal sealed class TextureState
    {
        public string Path;
        public int AndroidSize;
        public int IosSize;
    }
}