namespace D3B.Bot.Reports;

/// <summary>A read-only stream that tells how many bytes of it were read, which is how far a post has gone.</summary>
internal sealed class ProgressStream : Stream
{
    private readonly Stream _inner;
    private readonly IProgress<long>? _progress;
    private long _read;

    public ProgressStream(Stream inner, IProgress<long>? progress)
    {
        _inner = inner;
        _progress = progress;
    }

    public override bool CanRead => true;

    public override bool CanSeek => _inner.CanSeek;

    public override bool CanWrite => false;

    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var count = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

        Told(count);

        return count;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _inner.Read(buffer, offset, count);

        Told(read);

        return read;
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();

        base.Dispose(disposing);
    }

    private void Told(int count)
    {
        if (count <= 0)
            return;

        _read += count;
        _progress?.Report(_read);
    }
}
