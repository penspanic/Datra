using Datra.Attributes;

namespace Datra.Interfaces
{
    /// <summary>
    /// Optional capability on top of <see cref="IRawDataProvider"/>: provides an
    /// authoritative <see cref="DataFormat"/> per file, overriding the runtime's
    /// usual extension-based inference. Repositories consult this when the
    /// provider implements it so a YAML-path file whose content is JSON (e.g.
    /// normalized in <see cref="Datra.Bundles.DatraBundleBuilder.NormalizeToJson"/>)
    /// gets dispatched to the JSON serializer.
    /// </summary>
    public interface IFormatAwareRawDataProvider : IRawDataProvider
    {
        /// <summary>
        /// Returns the format the provider knows the file is stored as, or
        /// <c>null</c> to fall back to extension-based detection.
        /// </summary>
        DataFormat? GetFormat(string path);
    }
}
