using Amazon.S3;
using Amazon.S3.Model;

namespace SalonBooking.SalonApi;

public class S3ImageStorage(IAmazonS3 s3, IConfiguration configuration) : IImageStorage
{
    public async Task<string> UploadAsync(string name, Stream content, string contentType)
    {
        var bucket = configuration["Storage:S3:BucketName"]!;

        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = name,
            InputStream = content,
            ContentType = contentType
        });

        var baseUrl = configuration["Storage:S3:PublicBaseUrl"]
            ?? $"https://{bucket}.s3.{s3.Config.RegionEndpoint.SystemName}.amazonaws.com";
        var path = string.Join('/', name.Split('/').Select(Uri.EscapeDataString));

        return $"{baseUrl.TrimEnd('/')}/{path}";
    }
}
