namespace D3B.Data.Tests.Releases;

/// <summary>Sends the first bytes of its content, then nothing more, as a connection that stalls.</summary>
internal sealed class StallingStream : Stream
{
    private readonly byte[] _head;
    private int _sent;

    public StallingStream(byte[] head)
    {
        _head = head;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_sent < _head.Length)
        {
            var count = Math.Min(buffer.Length, _head.Length - _sent);

            _head.AsMemory(_sent, count).CopyTo(buffer);
            _sent += count;

            return count;
        }

        await Task.Delay(Timeout.Infinite, cancellationToken);

        return 0;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
}
