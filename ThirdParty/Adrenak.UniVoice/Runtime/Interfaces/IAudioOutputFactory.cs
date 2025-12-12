namespace Adrenak.UniVoice {
    /// <summary>
    /// An abstract factory that creates a concrete <see cref="IAudioOutput"/> 
    /// </summary>
    public interface IAudioOutputFactory<T> {
        /// <summary>
        /// Creates an instance of a concrete <see cref="IAudioOutput"/> class
        /// </summary>
        IAudioOutput Create(T peerId);
    }
}