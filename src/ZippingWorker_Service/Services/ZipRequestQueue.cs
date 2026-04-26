using System.Threading.Channels;
using ZippingWorker_Service.Zipping;

namespace ZippingWorker_Service.Services
{
    public class ZipRequest
    {
        public List<ZipFileEntry> Files { get; set; } = [];
        public string OutputArchivePath { get; set; } = string.Empty;
        public ArchiveCompressionLevel CompressionLevel { get; set; } = ArchiveCompressionLevel.ultra;
        public bool ValidateZipping { get; set; } = true;
        public bool DeleteInputFiles { get; set; } = false;
    }

    public class ZipFileEntry
    {
        public string SourcePath { get; set; } = string.Empty;
        public string ArchivePath { get; set; } = string.Empty;
        public string? Hash { get; set; }
    }

    public interface IZipRequestQueue
    {
        ValueTask EnqueueAsync(ZipRequest request, CancellationToken cancellationToken = default);
        ValueTask<ZipRequest> DequeueAsync(CancellationToken cancellationToken = default);
    }

    public class ZipRequestQueue : IZipRequestQueue
    {
        private readonly Channel<ZipRequest> _channel;

        public ZipRequestQueue()
        {
            _channel = Channel.CreateUnbounded<ZipRequest>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
        }

        public async ValueTask EnqueueAsync(ZipRequest request, CancellationToken cancellationToken = default)
        {
            await _channel.Writer.WriteAsync(request, cancellationToken);
        }

        public async ValueTask<ZipRequest> DequeueAsync(CancellationToken cancellationToken = default)
        {
            return await _channel.Reader.ReadAsync(cancellationToken);
        }
    }
}
