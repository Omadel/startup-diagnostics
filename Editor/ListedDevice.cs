namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     A device ready for the measure, as the tools of its platform list it.
    /// </summary>
    internal sealed class ListedDevice
    {
        public ListedDevice(string id, string name)
        {
            Id = id;
            Name = name;
        }

        /// <summary>
        ///     What <see cref="MeasureSettings.Device" /> takes: the serial on Android, the UDID on iOS.
        /// </summary>
        public string Id { get; }

        public string Name { get; }
    }
}