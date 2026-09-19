namespace FRLG.StarterTool.Core.Video;

public sealed class FrameBufferPool
{
    private readonly object _lock = new();
    private readonly Stack<byte[]> _free = new();

    public FrameBufferPool(long maxBytes)
    {
        MaxBytes = Math.Max(maxBytes, 0);
    }

    public long MaxBytes { get; }

    public long HeldBytes
    {
        get
        {
            lock (_lock) return _free.Count == 0 ? 0 : (long)_free.Count * _free.Peek().Length;
        }
    }

    public byte[] Rent(int length)
    {
        lock (_lock)
        {
            if (_free.Count > 0)
            {
                if (_free.Peek().Length == length) return _free.Pop();
                _free.Clear();
            }
        }
        return new byte[length];
    }

    public void Return(byte[] buffer)
    {
        lock (_lock)
        {
            if (_free.Count > 0 && _free.Peek().Length != buffer.Length) _free.Clear();
            if ((long)(_free.Count + 1) * buffer.Length > MaxBytes) return;
            _free.Push(buffer);
        }
    }

    public void Clear()
    {
        lock (_lock) _free.Clear();
    }
}
