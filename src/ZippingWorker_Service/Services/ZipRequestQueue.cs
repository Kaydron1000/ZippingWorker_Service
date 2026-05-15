using System.Threading.Channels;
using ZippingWorker_Service.Configuration;
using ZippingWorker_Service.Model;
using ZippingWorker_Service.Zipping;

namespace ZippingWorker_Service.Services
{
    public class ZipRequest
    {
        public List<ZipFileEntry> Files { get; set; } = new();
        public string OutputArchivePath { get; set; } = string.Empty;
        public ArchiveCompressionLevel CompressionLevel { get; set; } = ArchiveCompressionLevel.ultra;
        public ValidateEnumType ValidateZipping { get; set; } = ValidateEnumType.extract;
        public DeleteEnumType DeleteInputFiles { get; set; } = DeleteEnumType.none;

        /// <summary>
        /// Snapshot of the configuration at the time this request was created.
        /// Ensures in-flight requests continue with their original configuration.
        /// </summary>
        public ZippingWorker_ServiceConfigurationType Configuration { get; set; } = new();
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
