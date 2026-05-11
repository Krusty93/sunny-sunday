namespace SunnySunday.Server.Services;

public interface IMailDeliveryService
{
    Task SendRecapAsync(string toAddress, byte[] epubContent, string fileName, CancellationToken cancellationToken = default);

    Task SendTestEmailAsync(string toAddress, CancellationToken cancellationToken = default);
}
