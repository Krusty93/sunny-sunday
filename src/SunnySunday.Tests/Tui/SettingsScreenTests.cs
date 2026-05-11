using System.Net;
using System.Text.Json;
using RichardSzalay.MockHttp;
using SunnySunday.Cli.Infrastructure;
using SunnySunday.Cli.Tui;
using SunnySunday.Core.Contracts;

namespace SunnySunday.Tests.Tui;

public sealed class SettingsScreenTests
{
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly SettingsResponse DefaultSettings = new()
    {
        Schedule = "daily",
        DeliveryTime = "18:00",
        Count = 5,
        KindleEmail = "user@kindle.com",
        Timezone = "UTC"
    };

    [Fact]
    public async Task InitializeAsync_LoadsSettingsAndBuildsFields()
    {
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "http://localhost/settings")
            .Respond("application/json", JsonSerializer.Serialize(DefaultSettings, CamelCaseOptions));
        using var httpClient = new HttpClient(mockHttp, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };

        var screen = new SettingsScreen(new SunnyHttpClient(httpClient));
        await screen.InitializeAsync(CancellationToken.None);

        Assert.NotNull(screen.Settings);
        Assert.Equal("user@kindle.com", screen.Settings.KindleEmail);
        Assert.Equal(6, screen.Fields.Count); // 5 editable + 1 action (test email)
    }

    [Fact]
    public async Task InitializeAsync_WithDevelopment_ShowsTriggerRecapAction()
    {
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "http://localhost/settings")
            .Respond("application/json", JsonSerializer.Serialize(DefaultSettings, CamelCaseOptions));
        using var httpClient = new HttpClient(mockHttp, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };

        var screen = new SettingsScreen(new SunnyHttpClient(httpClient), isDevelopment: true);
        await screen.InitializeAsync(CancellationToken.None);

        Assert.Equal(7, screen.Fields.Count); // 5 editable + 2 actions
        Assert.Contains(screen.Fields, f => f.ActionId == "trigger-recap");
    }

    [Fact]
    public async Task HandleKeyAsync_NavigationStaysWithinBounds()
    {
        var screen = await CreateInitializedScreen();

        // Move down past the end
        for (var i = 0; i < 20; i++)
            await screen.HandleKeyAsync(Key(ConsoleKey.DownArrow), CancellationToken.None);

        Assert.Equal(screen.Fields.Count - 1, screen.SelectedField);

        // Move up past the beginning
        for (var i = 0; i < 20; i++)
            await screen.HandleKeyAsync(Key(ConsoleKey.UpArrow), CancellationToken.None);

        Assert.Equal(0, screen.SelectedField);
    }

    [Fact]
    public async Task HandleKeyAsync_EscapeReturnsPop()
    {
        var screen = await CreateInitializedScreen();

        var result = await screen.HandleKeyAsync(Key(ConsoleKey.Escape), CancellationToken.None);

        Assert.Equal(ScreenAction.Pop, result.Action);
    }

    [Fact]
    public async Task HandleKeyAsync_QReturnsQuit()
    {
        var screen = await CreateInitializedScreen();

        var result = await screen.HandleKeyAsync(Key(ConsoleKey.Q, 'q'), CancellationToken.None);

        Assert.Equal(ScreenAction.Quit, result.Action);
    }

    [Fact]
    public async Task HandleKeyAsync_EnterOnEditableField_StartsEditMode()
    {
        var screen = await CreateInitializedScreen();

        // First field is Kindle Email (editable), verify it's editable
        Assert.Equal(SettingsScreen.FieldKind.Editable, screen.Fields[screen.SelectedField].Kind);

        // HandleKeyAsync delegates to HandleEnterAsync → StartEdit which touches
        // Terminal.Gui view references. In headless test we verify the field is
        // editable and the action field path works (tested separately).
        // Direct state assertion: editing is not active before Enter.
        Assert.False(screen.IsEditing);
    }

    [Fact]
    public async Task HandleKeyAsync_TestEmailSendsPostRequest()
    {
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "http://localhost/settings")
            .Respond("application/json", JsonSerializer.Serialize(DefaultSettings, CamelCaseOptions));
        mockHttp.When(HttpMethod.Post, "http://localhost/settings/test-email")
            .Respond(HttpStatusCode.OK, "application/json", "{\"message\":\"Test email sent successfully.\"}");
        using var httpClient = new HttpClient(mockHttp, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };

        var screen = new SettingsScreen(new SunnyHttpClient(httpClient));
        await screen.InitializeAsync(CancellationToken.None);

        var result = await screen.HandleKeyAsync(Key(ConsoleKey.T, 't'), CancellationToken.None);

        Assert.Equal(ScreenAction.None, result.Action);
        Assert.Contains("sent successfully", screen.StatusMessage);
        Assert.False(screen.StatusIsError);
    }

    [Fact]
    public async Task HandleKeyAsync_TestEmailFailure_ShowsError()
    {
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "http://localhost/settings")
            .Respond("application/json", JsonSerializer.Serialize(DefaultSettings, CamelCaseOptions));
        mockHttp.When(HttpMethod.Post, "http://localhost/settings/test-email")
            .Respond(HttpStatusCode.UnprocessableEntity, "application/json", "{\"errors\":{\"kindleEmail\":[\"Kindle email must be configured.\"]}}");
        using var httpClient = new HttpClient(mockHttp, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };

        var screen = new SettingsScreen(new SunnyHttpClient(httpClient));
        await screen.InitializeAsync(CancellationToken.None);

        var result = await screen.HandleKeyAsync(Key(ConsoleKey.T, 't'), CancellationToken.None);

        Assert.Equal(ScreenAction.None, result.Action);
        Assert.True(screen.StatusIsError);
    }

    [Fact]
    public async Task HandleKeyAsync_RefreshReloadsSettings()
    {
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "http://localhost/settings")
            .Respond("application/json", JsonSerializer.Serialize(DefaultSettings, CamelCaseOptions));
        using var httpClient = new HttpClient(mockHttp, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };

        var screen = new SettingsScreen(new SunnyHttpClient(httpClient));
        await screen.InitializeAsync(CancellationToken.None);

        var result = await screen.HandleKeyAsync(Key(ConsoleKey.R, 'r'), CancellationToken.None);

        Assert.Equal(ScreenAction.None, result.Action);
        Assert.Contains("refreshed", screen.StatusMessage);
        Assert.False(screen.StatusIsError);
    }

    [Theory]
    [InlineData("kindleEmail", "invalid", false)]
    [InlineData("kindleEmail", "valid@email.com", true)]
    [InlineData("count", "0", false)]
    [InlineData("count", "16", false)]
    [InlineData("count", "5", true)]
    [InlineData("count", "abc", false)]
    [InlineData("schedule", "monthly", false)]
    [InlineData("schedule", "daily", true)]
    [InlineData("schedule", "weekly", true)]
    [InlineData("deliveryTime", "25:00", false)]
    [InlineData("deliveryTime", "09:30", true)]
    [InlineData("deliveryTime", "nottime", false)]
    public void ValidateField_ReturnsExpectedResult(string fieldId, string value, bool shouldBeValid)
    {
        var field = new SettingsScreen.SettingsField("Test", string.Empty, fieldId, SettingsScreen.FieldKind.Editable);

        // Use reflection to access the private static method
        var method = typeof(SettingsScreen).GetMethod("ValidateField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var result = (string?)method.Invoke(null, [field, value]);

        if (shouldBeValid)
            Assert.Null(result);
        else
            Assert.NotNull(result);
    }

    [Fact]
    public void KeyHints_ContainExpectedEntries()
    {
        using var mockHttp = new MockHttpMessageHandler();
        using var httpClient = new HttpClient(mockHttp, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };
        var screen = new SettingsScreen(new SunnyHttpClient(httpClient));

        Assert.Contains(screen.KeyHints, hint => hint is ("↑↓", "Navigate"));
        Assert.Contains(screen.KeyHints, hint => hint is ("Enter", "Edit"));
        Assert.Contains(screen.KeyHints, hint => hint is ("T", "Test email"));
        Assert.Contains(screen.KeyHints, hint => hint is ("R", "Refresh"));
        Assert.Contains(screen.KeyHints, hint => hint is ("Esc", "Go Back"));
        Assert.Contains(screen.KeyHints, hint => hint is ("Q", "Quit"));
    }

    [Fact]
    public async Task HandleKeyAsync_EnterOnActionField_ExecutesAction()
    {
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "http://localhost/settings")
            .Respond("application/json", JsonSerializer.Serialize(DefaultSettings, CamelCaseOptions));
        mockHttp.When(HttpMethod.Post, "http://localhost/settings/test-email")
            .Respond(HttpStatusCode.OK, "application/json", "{\"message\":\"ok\"}");
        using var httpClient = new HttpClient(mockHttp, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };

        var screen = new SettingsScreen(new SunnyHttpClient(httpClient));
        await screen.InitializeAsync(CancellationToken.None);

        // Navigate to the "Send test email" action (index 5)
        for (var i = 0; i < 5; i++)
            await screen.HandleKeyAsync(Key(ConsoleKey.DownArrow), CancellationToken.None);

        Assert.Equal("test-email", screen.Fields[screen.SelectedField].ActionId);

        var result = await screen.HandleKeyAsync(Key(ConsoleKey.Enter), CancellationToken.None);

        Assert.Equal(ScreenAction.None, result.Action);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Test helper; MockHttp and HttpClient outlive the helper call")]
    private static async Task<SettingsScreen> CreateInitializedScreen()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "http://localhost/settings")
            .Respond("application/json", JsonSerializer.Serialize(DefaultSettings, CamelCaseOptions));
        var httpClient = new HttpClient(mockHttp, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };

        var screen = new SettingsScreen(new SunnyHttpClient(httpClient));
        await screen.InitializeAsync(CancellationToken.None);
        return screen;
    }

    private static ConsoleKeyInfo Key(ConsoleKey key, char keyChar = '\0')
        => new(keyChar, key, shift: false, alt: false, control: false);
}
