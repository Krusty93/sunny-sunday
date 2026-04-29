using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using SunnySunday.Server.Infrastructure.Smtp;

namespace SunnySunday.Server.Services;

public sealed class MailDeliveryService : IMailDeliveryService
{
    private readonly SmtpSettings _settings;

    public MailDeliveryService(IOptions<SmtpSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendRecapAsync(string toAddress, byte[] epubContent, string fileName, CancellationToken cancellationToken = default)
    {
        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Sunny Sunday", _settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(toAddress));
        message.Subject = "Your Sunny Sunday Recap";

        var body = new TextPart("plain")
        {
            Text = "Your Kindle highlight recap is attached."
        };

        var attachment = new MimePart("application", "epub+zip")
        {
            Content = new MimeContent(new MemoryStream(epubContent)),
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            ContentTransferEncoding = ContentEncoding.Base64,
            FileName = fileName
        };

        var multipart = new Multipart("mixed") { body, attachment };
        message.Body = multipart;

        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.Host, _settings.Port, useSsl: false, cancellationToken);

        if (!string.IsNullOrEmpty(_settings.Username))
        {
            await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
