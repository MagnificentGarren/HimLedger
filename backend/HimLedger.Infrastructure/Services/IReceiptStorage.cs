namespace HimLedger.Infrastructure.Services;

public interface IReceiptStorage
{
    Task StoreAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken);
    Task<Uri> CreateReadSasAsync(string blobName, CancellationToken cancellationToken);
    Task DeleteIfExistsAsync(string blobName, CancellationToken cancellationToken);
}
