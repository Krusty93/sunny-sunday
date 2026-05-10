using System.Net;
using RichardSzalay.MockHttp;
using SunnySunday.Cli.Infrastructure;
using SunnySunday.Cli.Tui;
using SunnySunday.Cli.Tui.ViewModels;

namespace SunnySunday.Tests.Tui;

public sealed class HighlightDetailScreenTests
{
    [Fact]
    public async Task HandleKeyAsync_NavigatesWithinBounds()
    {
        using var mockHttp = new MockHttpMessageHandler();
        using var httpClient = new HttpClient(mockHttp, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };
        var screen = CreateScreen(new SunnyHttpClient(httpClient));

        await screen.HandleKeyAsync(Key(ConsoleKey.DownArrow), CancellationToken.None);
        await screen.HandleKeyAsync(Key(ConsoleKey.DownArrow), CancellationToken.None);
        await screen.HandleKeyAsync(Key(ConsoleKey.DownArrow), CancellationToken.None);

        Assert.Equal(1, screen.SelectedIndex);

        await screen.HandleKeyAsync(Key(ConsoleKey.UpArrow), CancellationToken.None);
        await screen.HandleKeyAsync(Key(ConsoleKey.UpArrow), CancellationToken.None);

        Assert.Equal(0, screen.SelectedIndex);
    }

    [Fact]
    public async Task HandleKeyAsync_EnterOpensActionMenu()
    {
        using var mockHttp = new MockHttpMessageHandler();
        using var httpClient = new HttpClient(mockHttp, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };
        var screen = CreateScreen(new SunnyHttpClient(httpClient));

        var result = await screen.HandleKeyAsync(Key(ConsoleKey.Enter), CancellationToken.None);

        Assert.Equal(ScreenAction.None, result.Action);
        Assert.True(screen.ActionMenuOpen);
        Assert.Equal(0, screen.ActionMenuIndex);
    }

    [Fact]
    public async Task HandleKeyAsync_EscapeClosesActionMenuBeforePopping()
    {
        using var mockHttp = new MockHttpMessageHandler();
        using var httpClient = new HttpClient(mockHttp, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };
        var screen = CreateScreen(new SunnyHttpClient(httpClient));
        await screen.HandleKeyAsync(Key(ConsoleKey.Enter), CancellationToken.None);

        var firstEscape = await screen.HandleKeyAsync(Key(ConsoleKey.Escape), CancellationToken.None);
        var secondEscape = await screen.HandleKeyAsync(Key(ConsoleKey.Escape), CancellationToken.None);

        Assert.Equal(ScreenAction.None, firstEscape.Action);
        Assert.False(screen.ActionMenuOpen);
        Assert.Equal(ScreenAction.Pop, secondEscape.Action);
    }

    [Fact]
    public async Task HandleKeyAsync_ActionMenuNavigationStaysWithinBounds()
    {
        using var mockHttp = new MockHttpMessageHandler();
        using var httpClient = new HttpClient(mockHttp, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };
        var screen = CreateScreen(new SunnyHttpClient(httpClient));
        await screen.HandleKeyAsync(Key(ConsoleKey.Enter), CancellationToken.None);

        for (var index = 0; index < 10; index++)
        {
            await screen.HandleKeyAsync(Key(ConsoleKey.DownArrow), CancellationToken.None);
        }

        Assert.Equal(4, screen.ActionMenuIndex);

        for (var index = 0; index < 10; index++)
        {
            await screen.HandleKeyAsync(Key(ConsoleKey.UpArrow), CancellationToken.None);
        }

        Assert.Equal(0, screen.ActionMenuIndex);
    }

    [Fact]
    public async Task HandleKeyAsync_ModifyWeightCallsApiAndUpdatesSelection()
    {
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.Expect(HttpMethod.Put, "http://localhost/highlights/11/weight")
            .Respond(HttpStatusCode.NoContent);
        using var httpClient = new HttpClient(mockHttp, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };

        var screen = CreateScreen(new SunnyHttpClient(httpClient));

        await screen.HandleKeyAsync(Key(ConsoleKey.Enter), CancellationToken.None);
        await screen.HandleKeyAsync(Key(ConsoleKey.Enter), CancellationToken.None);

        Assert.False(screen.ActionMenuOpen);
        Assert.Equal(4, screen.Highlights[0].Weight);
        mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task HandleKeyAsync_DeleteConfirmationDeletesHighlight()
    {
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.Expect(HttpMethod.Delete, "http://localhost/highlights/11")
            .Respond(HttpStatusCode.NoContent);
        using var httpClient = new HttpClient(mockHttp, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };

        var screen = CreateScreen(new SunnyHttpClient(httpClient));

        await screen.HandleKeyAsync(Key(ConsoleKey.Enter), CancellationToken.None);
        for (var index = 0; index < 4; index++)
        {
            await screen.HandleKeyAsync(Key(ConsoleKey.DownArrow), CancellationToken.None);
        }

        await screen.HandleKeyAsync(Key(ConsoleKey.Enter), CancellationToken.None);
        Assert.True(screen.DeleteConfirmationOpen);

        await screen.HandleKeyAsync(Key(ConsoleKey.Y, 'y'), CancellationToken.None);

        Assert.False(screen.DeleteConfirmationOpen);
        Assert.Single(screen.Highlights);
        Assert.DoesNotContain(screen.Highlights, highlight => highlight.Id == 11);
        mockHttp.VerifyNoOutstandingExpectation();
    }

    private static HighlightDetailScreen CreateScreen(SunnyHttpClient client)
        => new(
            new BookViewModel(
                101,
                201,
                "Book A",
                "Author A",
                2,
                false,
                false,
                [
                    new HighlightViewModel(11, 101, 201, "First highlight", "Book A", "Author A", false, null),
                    new HighlightViewModel(12, 101, 201, "Second highlight", "Book A", "Author A", false, 5)
                ]),
            client);

    private static ConsoleKeyInfo Key(ConsoleKey key, char keyChar = '\0')
        => new(keyChar, key, shift: false, alt: false, control: false);
}
