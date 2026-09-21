using System.Runtime.InteropServices;
using Sdcb.FFmpeg.Raw;

namespace FRLG.StarterTool.App.Capture;

internal sealed unsafe class MkvTail : IDisposable
{
    private const long KeepBytes = 256L * 1024 * 1024;

    private const long JoinBytes = 48L * 1024 * 1024;

    private const int HeaderScanBytes = 16 * 1024 * 1024;

    private const int IoBufferBytes = 64 * 1024;

    private sealed class Group
    {
        public readonly List<IntPtr> Packets = new();
        public double FirstPtsMs;
        public long Bytes;
    }

    private readonly string _path;
    private readonly object _lock = new();
    private readonly List<Group> _groups = new();
    private readonly Thread _thread;
    private volatile bool _stop;
    private long _bytes;
    private double _lastPtsMs = double.NaN;
    private double _timeBaseMs;
    private AVCodecParameters* _parameters;
    private AVCodecContext* _decoder;
    private SwsContext* _scaler;
    private int _scalerWidth, _scalerHeight, _scalerFormat;
    private GCHandle _self;

    private FileStream? _file;
    private long _headerEnd;
    private long _joinAt;
    private long _virtualPos;
    private avio_alloc_context_read_packet? _read;

    static MkvTail()
    {
        ffmpeg.av_log_set_level((int)LogLevel.Quiet);
    }

    public MkvTail(string path)
    {
        _path = path;
        _thread = new Thread(Follow) { IsBackground = true, Name = "Recording follower", Priority = ThreadPriority.BelowNormal };
    }

    public string Path => _path;

    public string? Error { get; private set; }

    public double? FramesPerSecond { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public double LastPtsMs
    {
        get { lock (_lock) return _lastPtsMs; }
    }

    public long LastGrowthTicks => Interlocked.Read(ref _lastGrowthTicks);

    private long _lastGrowthTicks;

    public event Action<double>? KeyframeRead;

    public void Start() => _thread.Start();

    private void Follow()
    {
        AVFormatContext* format = null;
        AVIOContext* io = null;
        _self = GCHandle.Alloc(this);
        try
        {
            if (!Open()) return;

            _read = ReadPacket;
            byte* buffer = (byte*)ffmpeg.av_malloc(IoBufferBytes);
            io = ffmpeg.avio_alloc_context(buffer, IoBufferBytes, 0, (void*)GCHandle.ToIntPtr(_self), _read, null, null);
            io->seekable = 0;

            format = ffmpeg.avformat_alloc_context();
            format->pb = io;
            format->flags |= (int)AVFMT_FLAG.CustomIo;
            AVInputFormat* matroska = ffmpeg.av_find_input_format("matroska");
            int opened = ffmpeg.avformat_open_input(&format, null, matroska, null);
            if (opened < 0)
            {
                format = null;
                if (!_stop) Error = "Not a readable recording";
                return;
            }

            int video = -1;
            for (int i = 0; i < (int)format->nb_streams; i++)
            {
                if (format->streams[i]->codecpar->codec_type == AVMediaType.Video)
                {
                    video = i;
                    break;
                }
            }
            if (video < 0)
            {
                Error = "No video in the recording";
                return;
            }

            AVStream* stream = format->streams[video];
            _timeBaseMs = 1000.0 * stream->time_base.Num / stream->time_base.Den;
            AVRational rate = stream->avg_frame_rate.Num > 0 ? stream->avg_frame_rate : stream->r_frame_rate;
            if (rate.Num > 0 && rate.Den > 0) FramesPerSecond = (double)rate.Num / rate.Den;
            Width = stream->codecpar->width;
            Height = stream->codecpar->height;
            lock (_lock)
            {
                _parameters = ffmpeg.avcodec_parameters_alloc();
                ffmpeg.avcodec_parameters_copy(_parameters, stream->codecpar);
            }

            AVPacket* packet = ffmpeg.av_packet_alloc();
            try
            {
                while (!_stop && ffmpeg.av_read_frame(format, packet) >= 0)
                {
                    if (packet->stream_index == video) Take(packet);
                    ffmpeg.av_packet_unref(packet);
                }
            }
            finally
            {
                ffmpeg.av_packet_free(&packet);
            }
        }
        catch (Exception e)
        {
            Error = "Recording reader failed: " + e.Message;
        }
        finally
        {
            if (format != null) ffmpeg.avformat_close_input(&format);
            if (io != null)
            {
                ffmpeg.av_freep(&io->buffer);
                ffmpeg.avio_context_free(&io);
            }
            _file?.Dispose();
            _file = null;
        }
    }

    private bool Open()
    {
        try
        {
            _file = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, IoBufferBytes);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Error = "Cannot open the recording";
            return false;
        }

        _headerEnd = long.MaxValue;
        _joinAt = 0;
        long length = _file.Length;
        if (length > JoinBytes * 2)
        {
            long first = FindCluster(0, HeaderScanBytes);
            long join = FindCluster(length - JoinBytes, (int)JoinBytes);
            if (first > 0 && join > first)
            {
                _headerEnd = first;
                _joinAt = join;
            }
        }
        _file.Position = 0;
        _virtualPos = 0;
        return true;
    }

    private long FindCluster(long from, int count)
    {
        FileStream file = _file!;
        var chunk = new byte[1024 * 1024 + 16];
        long at = from;
        long end = Math.Min(from + count, file.Length);
        while (at < end)
        {
            file.Position = at;
            int read = file.Read(chunk, 0, (int)Math.Min(chunk.Length, file.Length - at));
            if (read < 20) break;
            for (int i = 0; i + 19 < read; i++)
            {
                if (chunk[i] != 0x1F || chunk[i + 1] != 0x43 || chunk[i + 2] != 0xB6 || chunk[i + 3] != 0x75) continue;
                byte lead = chunk[i + 4];
                if (lead == 0) continue;
                int sizeBytes = 1;
                while ((lead & (0x80 >> (sizeBytes - 1))) == 0) sizeBytes++;
                int body = i + 4 + sizeBytes;
                if (chunk[body] == 0xBF && chunk[body + 1] == 0x84) body += 6;
                if (chunk[body] == 0xE7) return at + i;
            }
            at += read - 19;
        }
        return -1;
    }

    private static int ReadPacket(void* opaque, byte* buffer, int size)
    {
        var tail = (MkvTail)GCHandle.FromIntPtr((IntPtr)opaque).Target!;
        return tail.Read(buffer, size);
    }

    private int Read(byte* buffer, int size)
    {
        FileStream file = _file!;
        try
        {
            while (!_stop)
            {
                long position = _virtualPos;
                int want = size;
                if (position < _headerEnd && _headerEnd != long.MaxValue)
                {
                    want = (int)Math.Min(want, _headerEnd - position);
                }
                else if (_headerEnd != long.MaxValue && position == _headerEnd)
                {
                    position = _virtualPos = _joinAt;
                }

                file.Position = position;
                int read = file.Read(new Span<byte>(buffer, want));
                if (read > 0)
                {
                    Interlocked.Exchange(ref _lastGrowthTicks, Environment.TickCount64);
                    _virtualPos = position + read;
                    return read;
                }
                Thread.Sleep(50);
            }
        }
        catch (Exception)
        {
        }
        return ffmpeg.AVERROR_EOF;
    }

    private void Take(AVPacket* packet)
    {
        long stamp = packet->pts != ffmpeg.AV_NOPTS_VALUE ? packet->pts : packet->dts;
        if (stamp == ffmpeg.AV_NOPTS_VALUE) return;
        double ptsMs = stamp * _timeBaseMs;
        bool key = (packet->flags & ffmpeg.AV_PKT_FLAG_KEY) != 0;

        lock (_lock)
        {
            if (_stop) return;
            if (key || _groups.Count == 0)
            {
                if (!key) return;
                _groups.Add(new Group { FirstPtsMs = ptsMs });
            }
            Group group = _groups[^1];
            group.Packets.Add((IntPtr)ffmpeg.av_packet_clone(packet));
            group.Bytes += packet->size;
            group.FirstPtsMs = Math.Min(group.FirstPtsMs, ptsMs);
            _bytes += packet->size;
            if (double.IsNaN(_lastPtsMs) || ptsMs > _lastPtsMs) _lastPtsMs = ptsMs;

            while (_groups.Count > 2 && _bytes > KeepBytes) DropOldestLocked();
        }
        if (key) KeyframeRead?.Invoke(ptsMs);
    }

    private void DropOldestLocked()
    {
        Group group = _groups[0];
        _groups.RemoveAt(0);
        _bytes -= group.Bytes;
        foreach (IntPtr held in group.Packets)
        {
            var packet = (AVPacket*)held;
            ffmpeg.av_packet_free(&packet);
        }
    }

    public int Decode(double fromPtsMs, double toPtsMs, Func<double, Picture, bool> frame)
    {
        lock (_lock)
        {
            if (_parameters == null || _groups.Count == 0) return 0;
            if (!EnsureDecoderLocked()) return 0;

            int start = 0;
            for (int i = 0; i < _groups.Count; i++)
            {
                if (_groups[i].FirstPtsMs <= fromPtsMs) start = i;
            }

            ffmpeg.avcodec_flush_buffers(_decoder);
            AVFrame* decoded = ffmpeg.av_frame_alloc();
            int handed = 0;
            bool more = true;
            try
            {
                for (int g = start; g < _groups.Count && more; g++)
                {
                    if (_groups[g].FirstPtsMs > toPtsMs && g > start)
                    {
                        break;
                    }
                    foreach (IntPtr held in _groups[g].Packets)
                    {
                        if (ffmpeg.avcodec_send_packet(_decoder, (AVPacket*)held) < 0) continue;
                        more = Drain(decoded, fromPtsMs, toPtsMs, frame, ref handed);
                        if (!more) break;
                    }
                }
                if (more)
                {
                    ffmpeg.avcodec_send_packet(_decoder, null);
                    Drain(decoded, fromPtsMs, toPtsMs, frame, ref handed);
                }
            }
            finally
            {
                ffmpeg.av_frame_free(&decoded);
            }
            return handed;
        }
    }

    private bool Drain(AVFrame* decoded, double fromPtsMs, double toPtsMs, Func<double, Picture, bool> frame, ref int handed)
    {
        while (ffmpeg.avcodec_receive_frame(_decoder, decoded) >= 0)
        {
            long stamp = decoded->best_effort_timestamp != ffmpeg.AV_NOPTS_VALUE ? decoded->best_effort_timestamp : decoded->pts;
            double ptsMs = stamp * _timeBaseMs;
            bool go = true;
            if (ptsMs > toPtsMs) go = false;
            else if (ptsMs >= fromPtsMs)
            {
                handed++;
                go = frame(ptsMs, new Picture(this, decoded));
            }
            ffmpeg.av_frame_unref(decoded);
            if (!go) return false;
        }
        return true;
    }

    private bool EnsureDecoderLocked()
    {
        if (_decoder != null) return true;
        AVCodec* codec = ffmpeg.avcodec_find_decoder(_parameters->codec_id);
        if (codec == null)
        {
            Error = "No decoder for the recording's video";
            return false;
        }
        AVCodecContext* context = ffmpeg.avcodec_alloc_context3(codec);
        ffmpeg.avcodec_parameters_to_context(context, _parameters);
        context->thread_count = Math.Clamp(Environment.ProcessorCount / 2, 1, 8);
        if (ffmpeg.avcodec_open2(context, codec, null) < 0)
        {
            ffmpeg.avcodec_free_context(&context);
            Error = "The recording's video decoder would not open";
            return false;
        }
        _decoder = context;
        return true;
    }

    internal readonly struct Picture
    {
        private readonly MkvTail _tail;
        private readonly AVFrame* _frame;

        internal Picture(MkvTail tail, AVFrame* frame)
        {
            _tail = tail;
            _frame = frame;
        }

        public int Width => _frame->width;

        public int Height => _frame->height;

        public void ToBgra(Span<byte> target) => _tail.Convert(_frame, target);
    }

    private void Convert(AVFrame* frame, Span<byte> target)
    {
        if (target.Length < frame->width * frame->height * 4) throw new ArgumentException("Buffer too small.", nameof(target));

        if (_scaler == null || _scalerWidth != frame->width || _scalerHeight != frame->height || _scalerFormat != frame->format)
        {
            if (_scaler != null) ffmpeg.sws_freeContext(_scaler);
            _scaler = ffmpeg.sws_getContext(frame->width, frame->height, (AVPixelFormat)frame->format,
                frame->width, frame->height, AVPixelFormat.Bgra,
                (int)(SWS.Bilinear | SWS.AccurateRnd | SWS.FullChrHInt), null, null, null);
            if (_scaler == null) throw new InvalidOperationException("No conversion for the recording's pixel format.");
            _scalerWidth = frame->width;
            _scalerHeight = frame->height;
            _scalerFormat = frame->format;

            bool hd = frame->colorspace == AVColorSpace.Bt709
                || (frame->colorspace == AVColorSpace.Unspecified && frame->height >= 720);
            int* source = ffmpeg.sws_getCoefficients((int)(hd ? SWS_CS.Itu709 : SWS_CS.Itu601));
            int* output = ffmpeg.sws_getCoefficients((int)SWS_CS.Default);
            int fullRange = frame->color_range == AVColorRange.Jpeg ? 1 : 0;
            ffmpeg.sws_setColorspaceDetails(_scaler, source, fullRange, output, 1, 0, 1 << 16, 1 << 16);
        }

        fixed (byte* pixels = target)
        {
            byte*[] planes = { pixels, null, null, null };
            int[] strides = { frame->width * 4, 0, 0, 0 };
            ffmpeg.sws_scale(_scaler, frame->data.ToRawArray(), frame->linesize.ToArray(), 0, frame->height, planes, strides);
        }
    }

    public void Dispose()
    {
        _stop = true;
        if (_thread.IsAlive) _thread.Join(2000);
        lock (_lock)
        {
            while (_groups.Count > 0) DropOldestLocked();
            if (_decoder != null)
            {
                AVCodecContext* context = _decoder;
                ffmpeg.avcodec_free_context(&context);
                _decoder = null;
            }
            if (_scaler != null)
            {
                ffmpeg.sws_freeContext(_scaler);
                _scaler = null;
            }
            if (_parameters != null)
            {
                AVCodecParameters* parameters = _parameters;
                ffmpeg.avcodec_parameters_free(&parameters);
                _parameters = null;
            }
        }
        if (_self.IsAllocated && !_thread.IsAlive) _self.Free();
    }
}
