using Dapper;
using Microsoft.Data.Sqlite;
using SunnySunday.Server.Data;
using SunnySunday.Server.Infrastructure.Database;
using SunnySunday.Server.Models;
using SunnySunday.Server.Services;

namespace SunnySunday.Tests.Recap;

public sealed class HighlightSelectionServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection;
    private readonly RecapRepository _recapRepository;
    private readonly HighlightSelectionService _sut;
    private int _userId;

    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    public HighlightSelectionServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _recapRepository = new RecapRepository(_connection);
        _sut = new HighlightSelectionService(_recapRepository);
    }

    public async Task InitializeAsync()
    {
        await new SchemaBootstrap().ApplyAsync(_connection);
        _userId = await SeedUserAsync();
    }

    public Task DisposeAsync()
    {
        _connection.Dispose();
        return Task.CompletedTask;
    }

    // --- ComputeScore unit tests ---

    [Fact]
    public void ComputeScore_LastSeenNull_TreatsAsOldestAge()
    {
        var candidate = MakeCandidate(weight: 3, lastSeen: null);

        var score = HighlightSelectionService.ComputeScore(candidate, Now);

        var expectedAge = (int)(Now - DateTimeOffset.MinValue).TotalDays;
        Assert.Equal(expectedAge + 3, score);
    }

    [Fact]
    public void ComputeScore_RecentlySeenHighlight_HasLowerScore()
    {
        var yesterday = Now.AddDays(-1);
        var aMonthAgo = Now.AddDays(-30);

        var recent = MakeCandidate(weight: 3, lastSeen: yesterday);
        var older = MakeCandidate(weight: 3, lastSeen: aMonthAgo);

        var recentScore = HighlightSelectionService.ComputeScore(recent, Now);
        var olderScore = HighlightSelectionService.ComputeScore(older, Now);

        Assert.True(olderScore > recentScore);
    }

    [Fact]
    public void ComputeScore_HigherWeightIncreasesScore()
    {
        var sameLastSeen = Now.AddDays(-10);
        var low = MakeCandidate(weight: 1, lastSeen: sameLastSeen);
        var high = MakeCandidate(weight: 5, lastSeen: sameLastSeen);

        var lowScore = HighlightSelectionService.ComputeScore(low, Now);
        var highScore = HighlightSelectionService.ComputeScore(high, Now);

        Assert.Equal(4, highScore - lowScore);
    }

    // --- SelectAsync integration tests ---

    [Fact]
    public async Task SelectAsync_RanksHigherScoreFirst()
    {
        var settings = DefaultSettings(count: 3);

        // higher score: seen long ago + high weight
        var bookId = await SeedBookAsync("Book A", "Author A");
        var h1 = await SeedHighlightAsync(bookId, weight: 5, lastSeen: Now.AddDays(-100));
        // lower score: seen recently + low weight
        var h2 = await SeedHighlightAsync(bookId, weight: 1, lastSeen: Now.AddDays(-1));

        var results = await _sut.SelectAsync(_userId, settings, Now);

        Assert.Equal(2, results.Count);
        Assert.Equal(h1, results[0].Id);
        Assert.Equal(h2, results[1].Id);
    }

    [Fact]
    public async Task SelectAsync_TieBreak_MoreRecentCreatedAtFirst()
    {
        var settings = DefaultSettings(count: 2);
        var sameLastSeen = Now.AddDays(-10);
        var bookId = await SeedBookAsync("Book B", "Author B");

        // Both have same score (same lastSeen, same weight)
        // Newer one should come first
        var olderHighlightId = await SeedHighlightAsync(bookId, weight: 3, lastSeen: sameLastSeen,
            createdAt: Now.AddDays(-20));
        var newerHighlightId = await SeedHighlightAsync(bookId, weight: 3, lastSeen: sameLastSeen,
            createdAt: Now.AddDays(-5));

        var results = await _sut.SelectAsync(_userId, settings, Now);

        Assert.Equal(2, results.Count);
        Assert.Equal(newerHighlightId, results[0].Id);
        Assert.Equal(olderHighlightId, results[1].Id);
    }

    [Fact]
    public async Task SelectAsync_CapsResultToCount()
    {
        var settings = DefaultSettings(count: 2);
        var bookId = await SeedBookAsync("Book C", "Author C");

        await SeedHighlightAsync(bookId, weight: 3, lastSeen: Now.AddDays(-10));
        await SeedHighlightAsync(bookId, weight: 3, lastSeen: Now.AddDays(-20));
        await SeedHighlightAsync(bookId, weight: 3, lastSeen: Now.AddDays(-30));

        var results = await _sut.SelectAsync(_userId, settings, Now);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task SelectAsync_ExcludesHighlightLevelExclusion()
    {
        var settings = DefaultSettings(count: 10);
        var bookId = await SeedBookAsync("Book D", "Author D");

        var included = await SeedHighlightAsync(bookId, weight: 3, lastSeen: null);
        await SeedHighlightAsync(bookId, weight: 3, lastSeen: null, excluded: true);

        var results = await _sut.SelectAsync(_userId, settings, Now);

        Assert.All(results, r => Assert.Equal(included, r.Id));
        Assert.Single(results);
    }

    [Fact]
    public async Task SelectAsync_ExcludesBookLevelExclusion()
    {
        var settings = DefaultSettings(count: 10);
        var includedBookId = await SeedBookAsync("Included Book", "Author E");
        var excludedBookId = await SeedBookAsync("Excluded Book", "Author E");

        var included = await SeedHighlightAsync(includedBookId, weight: 3, lastSeen: null);
        await SeedHighlightAsync(excludedBookId, weight: 3, lastSeen: null);
        await ExcludeBookAsync(excludedBookId);

        var results = await _sut.SelectAsync(_userId, settings, Now);

        Assert.Single(results);
        Assert.Equal(included, results[0].Id);
    }

    [Fact]
    public async Task SelectAsync_ExcludesAuthorLevelExclusion()
    {
        var settings = DefaultSettings(count: 10);
        var includedBookId = await SeedBookAsync("Book F", "Author F");
        var excludedBookId = await SeedBookAsync("Book G", "Excluded Author");

        var included = await SeedHighlightAsync(includedBookId, weight: 3, lastSeen: null);
        await SeedHighlightAsync(excludedBookId, weight: 3, lastSeen: null);

        // Get author id of Excluded Author to exclude
        var authorId = await _connection.QuerySingleAsync<int>(
            "SELECT author_id FROM books WHERE id = @BookId",
            new { BookId = excludedBookId });
        await ExcludeAuthorAsync(authorId);

        var results = await _sut.SelectAsync(_userId, settings, Now);

        Assert.Single(results);
        Assert.Equal(included, results[0].Id);
    }

    [Fact]
    public async Task SelectAsync_NullLastSeen_TreatsAsOldestForRanking()
    {
        var settings = DefaultSettings(count: 2);
        var bookId = await SeedBookAsync("Book H", "Author H");

        // null last_seen should score as if never seen → very high age → ranked first
        var neverSeen = await SeedHighlightAsync(bookId, weight: 1, lastSeen: null);
        _ = await SeedHighlightAsync(bookId, weight: 5, lastSeen: Now.AddDays(-1));

        var results = await _sut.SelectAsync(_userId, settings, Now);

        // neverSeen has age = (Now - MinValue).TotalDays + 1, recently seen has age = 1 + 5
        // neverSeen wins easily
        Assert.Equal(neverSeen, results[0].Id);
    }

    [Fact]
    public async Task SelectAsync_EmptyDb_ReturnsEmpty()
    {
        var settings = DefaultSettings(count: 5);

        var results = await _sut.SelectAsync(_userId, settings, Now);

        Assert.Empty(results);
    }

    // --- Helpers ---

    private static SelectionCandidate MakeCandidate(int weight, DateTimeOffset? lastSeen) =>
        new(Id: 1, Text: "x", BookTitle: "b", AuthorName: "a", Weight: weight,
            LastSeen: lastSeen, CreatedAt: Now.AddDays(-10), Score: 0);

    private static Settings DefaultSettings(int count) => new() { Count = count };

    private async Task<int> SeedUserAsync()
    {
        return await _connection.QuerySingleAsync<int>(
            """
            INSERT INTO users (kindle_email, created_at) VALUES ('test@kindle.com', @Now);
            SELECT last_insert_rowid();
            """,
            new { Now = Now.ToString("O") });
    }

    private async Task<int> SeedBookAsync(string title, string authorName)
    {
        var authorId = await _connection.QuerySingleAsync<int>(
            """
            INSERT OR IGNORE INTO authors (name) VALUES (@Name);
            SELECT id FROM authors WHERE name = @Name;
            """,
            new { Name = authorName });

        return await _connection.QuerySingleAsync<int>(
            """
            INSERT INTO books (user_id, author_id, title) VALUES (@UserId, @AuthorId, @Title);
            SELECT last_insert_rowid();
            """,
            new { UserId = _userId, AuthorId = authorId, Title = title });
    }

    private async Task<int> SeedHighlightAsync(
        int bookId,
        int weight,
        DateTimeOffset? lastSeen,
        bool excluded = false,
        DateTimeOffset? createdAt = null)
    {
        var created = (createdAt ?? Now.AddDays(-7)).ToString("O");
        var lastSeenText = lastSeen?.ToString("O");

        return await _connection.QuerySingleAsync<int>(
            """
            INSERT INTO highlights (user_id, book_id, text, weight, excluded, last_seen, created_at)
            VALUES (@UserId, @BookId, @Text, @Weight, @Excluded, @LastSeen, @CreatedAt);
            SELECT last_insert_rowid();
            """,
            new
            {
                UserId = _userId,
                BookId = bookId,
                Text = $"highlight-{Guid.NewGuid():N}",
                Weight = weight,
                Excluded = excluded ? 1 : 0,
                LastSeen = lastSeenText,
                CreatedAt = created
            });
    }

    private Task ExcludeBookAsync(int bookId) =>
        _connection.ExecuteAsync(
            "INSERT INTO excluded_books (user_id, book_id, excluded_at) VALUES (@UserId, @BookId, @Now)",
            new { UserId = _userId, BookId = bookId, Now = Now.ToString("O") });

    private Task ExcludeAuthorAsync(int authorId) =>
        _connection.ExecuteAsync(
            "INSERT INTO excluded_authors (user_id, author_id, excluded_at) VALUES (@UserId, @AuthorId, @Now)",
            new { UserId = _userId, AuthorId = authorId, Now = Now.ToString("O") });
}
