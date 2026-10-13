namespace SalonBooking.SalonApi;

public interface IImageStorage
{
    Task<string> UploadAsync(string name, Stream content, string contentType);
}
