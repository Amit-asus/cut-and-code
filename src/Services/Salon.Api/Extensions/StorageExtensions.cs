using Amazon.S3;

namespace SalonBooking.SalonApi;

public static class StorageExtensions
{
    public static void AddImageStorage(this WebApplicationBuilder builder)
    {
        if (builder.Configuration["Storage:Provider"] == "S3")
        {
            if (string.IsNullOrWhiteSpace(builder.Configuration["Storage:S3:BucketName"]))
            {
                throw new InvalidOperationException("Storage:S3:BucketName is not configured.");
            }

            builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client());
            builder.Services.AddSingleton<IImageStorage, S3ImageStorage>();
        }
        else
        {
            builder.AddAzureBlobContainerClient("salon-images");
            builder.Services.AddSingleton<IImageStorage, AzureBlobImageStorage>();
        }
    }
}
