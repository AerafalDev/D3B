namespace D3B.Network.Captures;

/// <summary>What a record of a capture holds: bytes one side sent, or that side closing.</summary>
public enum CaptureRecordKind : byte
{
    /// <summary>Bytes the client sent, as one read gave them.</summary>
    FromClient = 1,

    /// <summary>Bytes the server sent, as one read gave them.</summary>
    FromServer = 2,

    /// <summary>The client closed its side, or its connection failed.</summary>
    ClientClosed = 3,

    /// <summary>The server closed its side, or its connection failed.</summary>
    ServerClosed = 4,
}
