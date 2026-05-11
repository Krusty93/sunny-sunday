using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using SunnySunday.Server.Infrastructure.Smtp;

namespace SunnySunday.Server.Services;

/// <summary>
/// Mail delivery service for the Development environment.
/// Connects to a local SMTP relay (e.g. smtp4dev) without TLS and without credentials.
/// The sender address is always "sunny-sunday" regardless of configuration.
/// </summary>
public sealed class DevMailDeliveryService : IMailDeliveryService
{
    private readonly SmtpSettings _settings;

    public DevMailDeliveryService(IOptions<SmtpSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendRecapAsync(string toAddress, byte[] epubContent, string fileName, CancellationToken cancellationToken = default)
    {
        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Sunny Sunday", "sunny-sunday"));
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
        await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.None, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }

    public async Task SendTestEmailAsync(string toAddress, CancellationToken cancellationToken = default)
    {
        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Sunny Sunday", "sunny-sunday"));
        message.To.Add(MailboxAddress.Parse(toAddress));
        message.Subject = "Sunny Sunday - Test Email";
        message.Body = new TextPart("plain")
        {
            Text = "This is a test email from Sunny Sunday. If you received this, your SMTP configuration is working correctly."
        };

        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.None, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
