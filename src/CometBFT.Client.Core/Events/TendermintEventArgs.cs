namespace CometBFT.Client.Core.Events;

/// <summary>
/// Provides event data for Tendermint domain events.
/// </summary>
/// <typeparam name="T">The type of the domain value carried by the event.</typeparam>
public sealed class TendermintEventArgs<T> : EventArgs
{
    /// <summary>
    /// Gets the domain value associated with the event.
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="TendermintEventArgs{T}"/> with the specified value.
    /// </summary>
    /// <param name="value">The domain value.</param>
    public TendermintEventArgs(T value) => Value = value;
}
