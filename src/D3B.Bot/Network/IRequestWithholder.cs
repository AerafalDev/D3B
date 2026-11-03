using Google.Protobuf;

namespace D3B.Bot.Network;

/// <summary>Tells a game session which of the client's requests not to send the server (D367).</summary>
/// <remarks>
/// Called from the client's read loop, as an observer is: it must be quick and safe across threads. While it withholds,
/// the relay holds each of the client's frames until it is whole, so that one withheld goes nowhere.
/// </remarks>
public interface IRequestWithholder
{
    /// <summary>Gets whether it may withhold the client's next request.</summary>
    bool IsWithholding { get; }

    /// <summary>Tells whether a request the client sent must not reach the server.</summary>
    /// <param name="request">The request, readable.</param>
    /// <returns>Whether to withhold it.</returns>
    bool Withholds(IMessage request);
}
