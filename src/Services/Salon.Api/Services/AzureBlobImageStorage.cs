using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace SalonBooking.SalonApi;

public class AzureBlobImageStorage(BlobContainerClient container) : IImageStorage
{
    public async Task<string> UploadAsync(string name, Stream content, string contentType)
    {
        await container.CreateIfNotExistsAsync();
        await container.SetAccessPolicyAsync(PublicAccessType.Blob);

        var blob = container.GetBlobClient(name);
        await blob.UploadAsync(content, overwrite: true);

        return blob.Uri.ToString();
    }
}
