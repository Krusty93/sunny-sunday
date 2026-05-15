using Microsoft.Extensions.Logging;
using Relego.Cli.Infrastructure;
using Relego.Cli.Parsing;
using Relego.Core.Contracts;

namespace Relego.Cli.Sync;

public sealed class ClippingsSyncWorkflow(RelegoHttpClient client, ILogger<ClippingsSyncWorkflow> logger)
{
    public async Task<ClippingsSyncOutcome> ExecuteAsync(ClippingsSyncOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var filePath = await ResolveFilePathAsync(options, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return ClippingsSyncOutcome.Cancelled();
        }

        filePath = filePath.Trim();
        logger.LogDebug("Resolved clippings path: {FilePath}", filePath);

        if (!File.Exists(filePath))
        {
            return ClippingsSyncOutcome.FileNotFound(filePath);
        }

        ParseResult parseResult;

        try
        {
            var parseAsync = options.ParseAsync;
            parseResult = parseAsync is null
                ? await ClippingsParser.ParseAsync(filePath, options.ParserLogger).ConfigureAwait(false)
                : await parseAsync(filePath, options.ParserLogger).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to parse clippings file {FilePath}", filePath);
            return ClippingsSyncOutcome.ParseFailed(filePath, ex);
        }

        if (parseResult.Books.Count == 0)
        {
            return ClippingsSyncOutcome.NoHighlightsFound(filePath, parseResult);
        }

        var request = CreateSyncRequest(parseResult);
        logger.LogDebug(
            "Sending {BookCount} books with {HighlightCount} highlights to server",
            request.Books.Count,
            request.Books.Sum(book => book.Highlights.Count));

        try
        {
            var response = await client.PostSyncAsync(request, cancellationToken).ConfigureAwait(false);
            return ClippingsSyncOutcome.Succeeded(filePath, parseResult, response);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Failed to sync clippings from {FilePath}", filePath);
            return ClippingsSyncOutcome.ServerError(filePath, parseResult, ex);
        }
    }

    private static async Task<string?> ResolveFilePathAsync(ClippingsSyncOptions options, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.FilePath))
        {
            return options.FilePath;
        }

        var detectedPath = KindleDetector.DetectClippingsPath();
        if (options.ResolvePathAsync is not null)
        {
            return await options.ResolvePathAsync(new ClippingsPathPromptRequest(detectedPath), cancellationToken).ConfigureAwait(false);
        }

        return detectedPath;
    }

    private static SyncRequest CreateSyncRequest(ParseResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new SyncRequest
        {
            Books = result.Books.Select(book => new SyncBookRequest
            {
                Title = book.Title,
                Author = book.Author,
                Highlights = book.Highlights.Select(highlight => new SyncHighlightRequest
                {
                    Text = highlight.Text,
                    AddedOn = highlight.AddedOn
                }).ToList()
            }).ToList()
        };
    }
}

public sealed record ClippingsSyncOptions
{
    public string? FilePath { get; init; }

    public Func<ClippingsPathPromptRequest, CancellationToken, ValueTask<string?>>? ResolvePathAsync { get; init; }

    public Func<string, ILogger?, Task<ParseResult>>? ParseAsync { get; init; }

    public ILogger? ParserLogger { get; init; }
}

public sealed record ClippingsPathPromptRequest(string? DetectedPath);

public enum ClippingsSyncStatus
{
    Cancelled,
    FileNotFound,
    ParseFailed,
    NoHighlightsFound,
    ServerError,
    Succeeded
}

public sealed record ClippingsSyncOutcome
{
    public required ClippingsSyncStatus Status { get; init; }

    public string? FilePath { get; init; }

    public string? Message { get; init; }

    public ParseResult? ParseResult { get; init; }

    public SyncResponse? Response { get; init; }

    public Exception? Error { get; init; }

    public int TotalHighlightsParsed => ParseResult?.Books.Sum(book => book.Highlights.Count) ?? 0;

    public bool IsSuccessful => Status is ClippingsSyncStatus.NoHighlightsFound or ClippingsSyncStatus.Succeeded;

    public static ClippingsSyncOutcome Cancelled() => new()
    {
        Status = ClippingsSyncStatus.Cancelled,
        Message = "Sync cancelled."
    };

    public static ClippingsSyncOutcome FileNotFound(string filePath) => new()
    {
        Status = ClippingsSyncStatus.FileNotFound,
        FilePath = filePath,
        Message = $"File not found: {filePath}"
    };

    public static ClippingsSyncOutcome ParseFailed(string filePath, Exception error) => new()
    {
        Status = ClippingsSyncStatus.ParseFailed,
        FilePath = filePath,
        Message = error.Message,
        Error = error
    };

    public static ClippingsSyncOutcome NoHighlightsFound(string filePath, ParseResult parseResult) => new()
    {
        Status = ClippingsSyncStatus.NoHighlightsFound,
        FilePath = filePath,
        Message = "No highlights found in the clippings file.",
        ParseResult = parseResult
    };

    public static ClippingsSyncOutcome ServerError(string filePath, ParseResult parseResult, HttpRequestException error) => new()
    {
        Status = ClippingsSyncStatus.ServerError,
        FilePath = filePath,
        Message = error.Message,
        ParseResult = parseResult,
        Error = error
    };

    public static ClippingsSyncOutcome Succeeded(string filePath, ParseResult parseResult, SyncResponse response) => new()
    {
        Status = ClippingsSyncStatus.Succeeded,
        FilePath = filePath,
        ParseResult = parseResult,
        Response = response
    };
}
