using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;

namespace HimLedger.Infrastructure.Services;

public sealed class AzureReceiptStorage(IConfiguration configuration) : IReceiptStorage
{
    private const string ContainerSetting = "AzureBlobStorage:ContainerName";

    public async Task StoreAsync(
        string blobName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        var container = GetContainer();
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        await container.GetBlobClient(blobName).UploadAsync(
            content,
            new BlobHttpHeaders { ContentType = contentType },
            cancellationToken: cancellationToken);
    }

    public async Task<Uri> CreateReadSasAsync(string blobName, CancellationToken cancellationToken)
    {
        var blob = GetContainer().GetBlobClient(blobName);
        if (!await blob.ExistsAsync(cancellationToken))
        {
            throw new KeyNotFoundException("The requested receipt does not exist.");
        }

        if (!blob.CanGenerateSasUri)
        {
            throw new InvalidOperationException(
                "Azure Blob Storage must use a connection string with a shared account key to create receipt SAS URLs.");
        }

        var sas = new BlobSasBuilder
        {
            BlobContainerName = blob.BlobContainerName,
            BlobName = blob.Name,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-1),
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(5),
            ContentDisposition = "attachment"
        };
        sas.SetPermissions(BlobSasPermissions.Read);
        return blob.GenerateSasUri(sas);
    }

    public async Task DeleteIfExistsAsync(string blobName, CancellationToken cancellationToken)
    {
        await GetContainer().GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    private BlobContainerClient GetContainer()
    {
        var connectionString = configuration.GetConnectionString("AzureBlobStorage");
        var containerName = configuration[ContainerSetting];
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(containerName))
        {
            throw new InvalidOperationException(
                "Configure ConnectionStrings:AzureBlobStorage and AzureBlobStorage:ContainerName before using receipt storage.");
        }

        return new BlobServiceClient(connectionString).GetBlobContainerClient(containerName);
    }
}
