using GSYNC.Core.Utilities;
using Xunit;

namespace GSYNC.Core.Tests;

public sealed class AtomicFileTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gsync-atomic-tests", Guid.NewGuid().ToString("N"));

    public AtomicFileTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptedCopy_PreservesDestination_AndCleansStagingFile(bool cancel)
    {
        var target = Path.Combine(_root, "save.sav");
        await File.WriteAllTextAsync(target, "original");
        using var cts = new CancellationTokenSource();
        using var content = new FailingReadStream("replacement"u8.ToArray(), cancel ? cts.Cancel : null);
        await Assert.ThrowsAnyAsync<Exception>(() => AtomicFile.CopyAsync(content, target, cts.Token));
        Assert.Equal("original", await File.ReadAllTextAsync(target));
        Assert.Equal(new[] { target }, Directory.GetFiles(_root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedCommit_RollsBackEarlierReplacementOrDeletion(bool delete)
    {
        var first = Path.Combine(_root, "first.sav");
        var blocked = Path.Combine(_root, "directory");
        Directory.CreateDirectory(blocked);
        await File.WriteAllTextAsync(first, "original");
        using var one = delete ? new AtomicFile.PreparedFile(first, null)
            : await AtomicFile.PrepareAsync(new MemoryStream("replacement"u8.ToArray()), first, CancellationToken.None);
        using var two = await AtomicFile.PrepareAsync(new MemoryStream("second"u8.ToArray()), blocked, CancellationToken.None);
        Assert.Throws<IOException>(() => AtomicFile.Apply(new[] { one, two }));
        Assert.Equal("original", await File.ReadAllTextAsync(first));
        Assert.True(Directory.Exists(blocked));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}

internal sealed class FailingReadStream(byte[] data, Action? afterFirstRead = null) : Stream
{
    private int _read;
    public override bool CanRead => true;
    public override bool CanWrite => false;
    public override bool CanSeek => false;
    public override long Length => data.Length;
    public override long Position { get => _read; set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_read > 0) throw new IOException("Simulated interrupted transfer.");
        var bytes = Math.Min(count, Math.Min(data.Length, 3));
        data.AsSpan(0, bytes).CopyTo(buffer.AsSpan(offset));
        _read += bytes;
        afterFirstRead?.Invoke();
        return bytes;
    }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = new byte[buffer.Length];
        var count = Read(bytes, 0, bytes.Length);
        bytes.AsMemory(0, count).CopyTo(buffer);
        return ValueTask.FromResult(count);
    }
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
