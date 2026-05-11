using System.Globalization;
using SunnySunday.Cli.Infrastructure;
using SunnySunday.Core.Contracts;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace SunnySunday.Cli.Tui;

public sealed class SettingsScreen : IScreen
{
    private const int LabelColumnWidth = 22;
    private const int HorizontalPadding = 2;
    private const int MinWeight = 1;
    private const int MaxWeight = 15;

    private static readonly (string Key, string Label)[] NormalKeyHints =
    [
        ("↑↓", "Navigate"),
        ("Enter", "Edit"),
        ("T", "Test email"),
        ("R", "Refresh"),
        ("Esc", "Go Back"),
        ("Q", "Quit")
    ];

    private readonly SunnyHttpClient _client;
    private readonly bool _isDevelopment;
    private readonly List<SettingsField> _fields = [];
    private SettingsResponse? _settings;
    private int _selectedField;
    private bool _isEditing;
    private string _editBuffer = string.Empty;
    private string? _statusMessage;
    private bool _statusIsError;

    // Terminal.Gui view references
    private Label? _statusLabel;
    private ListView? _fieldList;
    private System.Collections.ObjectModel.ObservableCollection<string>? _fieldRows;
    private TextField? _editField;
    private Label? _editPromptLabel;
    private View? _editOverlay;
    private Action<ScreenResult>? _navigate;
    private bool _viewCreated;

    public SettingsScreen(SunnyHttpClient client, bool isDevelopment = false)
    {
        _client = client;
        _isDevelopment = isDevelopment;
    }

    public string Title => "";

    public int SelectedField => _selectedField;

    public bool IsEditing => _isEditing;

    public string EditBuffer => _editBuffer;

    public string? StatusMessage => _statusMessage;

    public bool StatusIsError => _statusIsError;

    public SettingsResponse? Settings => _settings;

    public IReadOnlyList<SettingsField> Fields => _fields;

    public IReadOnlyList<(string Key, string Label)> KeyHints => NormalKeyHints;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        _settings = await _client.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        RebuildFields();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Views are owned by the parent container hierarchy")]
    public View CreateView(Action<ScreenResult> navigate)
    {
        ArgumentNullException.ThrowIfNull(navigate);
        _navigate = navigate;

        var container = new View
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true
        };

        var headerLabel = new Label
        {
            X = HorizontalPadding,
            Y = 0,
            Width = Dim.Fill(HorizontalPadding * 2),
            Height = 1,
            Text = "Settings",
            CanFocus = false
        };
        headerLabel.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(
            new Color(110, 200, 255), StatusChrome.Background)));

        _fieldRows = new System.Collections.ObjectModel.ObservableCollection<string>();
        _fieldList = new ShortcutListView
        {
            X = HorizontalPadding,
            Y = 2,
            Width = Dim.Fill(HorizontalPadding * 2),
            Height = Dim.Fill(3),
            CanFocus = true
        };
        _fieldList.SetSource(_fieldRows);
        _fieldList.ValueChanged += (_, _) =>
        {
            if (!_isEditing && _fieldList.SelectedItem is int sel)
                _selectedField = Math.Clamp(sel, 0, Math.Max(0, _fields.Count - 1));
        };
        _fieldList.Accepting += async (_, _) => await HandleFieldEnterAsync().ConfigureAwait(false);
        if (_fieldList is ShortcutListView shortcutList)
        {
            shortcutList.ShortcutKeyPressed += async (_, key) => await HandleShortcutKeyAsync(key).ConfigureAwait(false);
        }
        _fieldList.KeyDown += async (_, key) => await HandleListKeyDownAsync(key).ConfigureAwait(false);

        _editPromptLabel = new Label
        {
            X = HorizontalPadding,
            Y = Pos.AnchorEnd(3),
            Width = Dim.Fill(HorizontalPadding * 2),
            Height = 1,
            Visible = false,
            CanFocus = false
        };
        _editPromptLabel.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(
            new Color(150, 190, 230), StatusChrome.Background)));

        _editField = new TextField
        {
            X = HorizontalPadding,
            Y = Pos.AnchorEnd(2),
            Width = Dim.Fill(HorizontalPadding * 2),
            Height = 1,
            Visible = false,
            CanFocus = true
        };
        _editField.Accepting += async (_, _) => await HandleEditSubmitAsync().ConfigureAwait(false);
        _editField.KeyDown += (_, key) =>
        {
            if (key.KeyCode == KeyCode.Esc)
            {
                CancelEdit();
                key.Handled = true;
            }
        };

        _editOverlay = new View
        {
            X = 0,
            Y = Pos.AnchorEnd(3),
            Width = Dim.Fill(),
            Height = 3,
            Visible = false,
            CanFocus = false
        };

        _statusLabel = new Label
        {
            X = HorizontalPadding,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(HorizontalPadding * 2),
            Height = 1,
            Visible = false,
            CanFocus = false
        };

        container.KeyDown += async (_, key) => await HandleContainerKeyDownAsync(key).ConfigureAwait(false);

        container.Add(headerLabel, _fieldList, _editPromptLabel, _editField, _editOverlay, _statusLabel);

        _viewCreated = true;
        UpdateViewState();

        return container;
    }

    public async Task<ScreenResult> HandleKeyAsync(ConsoleKeyInfo key, CancellationToken cancellationToken)
    {
        if (_isEditing)
        {
            // Edit mode keys are handled by TextField events
            return ScreenResult.Stay();
        }

        ScreenResult result = key.Key switch
        {
            ConsoleKey.UpArrow => MoveSelection(-1),
            ConsoleKey.DownArrow => MoveSelection(1),
            ConsoleKey.Enter => await HandleEnterAsync(cancellationToken).ConfigureAwait(false),
            ConsoleKey.T => await HandleTestEmailAsync(cancellationToken).ConfigureAwait(false),
            ConsoleKey.R => await HandleRefreshAsync(cancellationToken).ConfigureAwait(false),
            ConsoleKey.Q => ScreenResult.Quit(),
            ConsoleKey.Escape => ScreenResult.Pop(),
            ConsoleKey.C when key.Modifiers.HasFlag(ConsoleModifiers.Control) => ScreenResult.Quit(),
            _ => ScreenResult.Stay()
        };

        UpdateViewStateIfCreated();
        return result;
    }

    private ScreenResult MoveSelection(int delta)
    {
        if (_fields.Count == 0)
            return ScreenResult.Stay();
        _selectedField = Math.Clamp(_selectedField + delta, 0, _fields.Count - 1);
        return ScreenResult.Stay();
    }

    private async Task<ScreenResult> HandleEnterAsync(CancellationToken cancellationToken)
    {
        if (_fields.Count == 0 || _selectedField < 0 || _selectedField >= _fields.Count)
            return ScreenResult.Stay();

        var field = _fields[_selectedField];

        if (field.Kind == FieldKind.Action)
        {
            return await ExecuteActionAsync(field, cancellationToken).ConfigureAwait(false);
        }

        StartEdit(field);
        return ScreenResult.Stay();
    }

    private void StartEdit(SettingsField field)
    {
        _isEditing = true;
        _editBuffer = field.Value;

        if (_editPromptLabel is not null)
        {
            _editPromptLabel.Text = $"Edit {field.Label}:";
            _editPromptLabel.Visible = true;
        }

        if (_editField is not null)
        {
            _editField.Text = _editBuffer;
            _editField.Visible = true;
            _editField.SetFocus();
            _editField.MoveEnd();
        }

        if (_editOverlay is not null)
            _editOverlay.Visible = true;

        UpdateViewStateIfCreated();
    }

    private void CancelEdit()
    {
        _isEditing = false;
        _editBuffer = string.Empty;

        if (_editPromptLabel is not null)
            _editPromptLabel.Visible = false;
        if (_editField is not null)
            _editField.Visible = false;
        if (_editOverlay is not null)
            _editOverlay.Visible = false;

        _fieldList?.SetFocus();
        UpdateViewStateIfCreated();
    }

    private async Task HandleEditSubmitAsync()
    {
        if (!_isEditing || _fields.Count == 0)
            return;

        var field = _fields[_selectedField];
        var newValue = _editField?.Text?.Trim() ?? string.Empty;

        var validationError = ValidateField(field, newValue);
        if (validationError is not null)
        {
            SetStatus(validationError, isError: true);
            return;
        }

        var request = BuildUpdateRequest(field, newValue);

        try
        {
            _settings = await _client.PutSettingsAsync(request).ConfigureAwait(false);
            RebuildFields();
            CancelEdit();
            SetStatus($"{field.Label} updated.", isError: false);
        }
        catch (HttpRequestException ex)
        {
            SetStatus($"Failed to save: {ex.Message}", isError: true);
        }
    }

    private async Task<ScreenResult> HandleTestEmailAsync(CancellationToken cancellationToken)
    {
        SetStatus("Sending test email...", isError: false);
        UpdateViewStateIfCreated();

        try
        {
            using var response = await _client.PostTestEmailAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                SetStatus("Test email sent successfully.", isError: false);
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                SetStatus($"Test email failed: {body}", isError: true);
            }
        }
        catch (HttpRequestException ex)
        {
            SetStatus($"Test email failed: {ex.Message}", isError: true);
        }

        return ScreenResult.Stay();
    }

    private async Task<ScreenResult> HandleRefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            _settings = await _client.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
            RebuildFields();
            SetStatus("Settings refreshed.", isError: false);
        }
        catch (HttpRequestException ex)
        {
            SetStatus($"Refresh failed: {ex.Message}", isError: true);
        }

        return ScreenResult.Stay();
    }

    private async Task<ScreenResult> ExecuteActionAsync(SettingsField field, CancellationToken cancellationToken)
    {
        if (field.ActionId == "test-email")
        {
            return await HandleTestEmailAsync(cancellationToken).ConfigureAwait(false);
        }

        if (field.ActionId == "trigger-recap")
        {
            SetStatus("Triggering recap...", isError: false);
            UpdateViewStateIfCreated();

            try
            {
                using var response = await _client.PostTestRecapAsync(cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                    SetStatus("Recap triggered.", isError: false);
                else
                    SetStatus("Recap trigger failed.", isError: true);
            }
            catch (HttpRequestException ex)
            {
                SetStatus($"Recap trigger failed: {ex.Message}", isError: true);
            }

            return ScreenResult.Stay();
        }

        return ScreenResult.Stay();
    }

    private static string? ValidateField(SettingsField field, string value)
    {
        return field.FieldId switch
        {
            "kindleEmail" => !value.Contains('@') ? "Email must contain '@'." : null,
            "schedule" => value is not "daily" and not "weekly" ? "Schedule must be 'daily' or 'weekly'." : null,
            "deliveryTime" => !TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                ? "Time must be in HH:mm format." : null,
            "count" => !int.TryParse(value, out var c) || c < MinWeight || c > MaxWeight
                ? $"Count must be an integer between {MinWeight} and {MaxWeight}." : null,
            "timezone" => !IsValidTimezone(value) ? "Invalid IANA timezone identifier." : null,
            _ => null
        };
    }

    private static bool IsValidTimezone(string value)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(value.Trim());
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
    }

    private static UpdateSettingsRequest BuildUpdateRequest(SettingsField field, string value)
    {
        return field.FieldId switch
        {
            "kindleEmail" => new UpdateSettingsRequest { KindleEmail = value },
            "schedule" => new UpdateSettingsRequest { Schedule = value },
            "deliveryTime" => new UpdateSettingsRequest { DeliveryTime = value },
            "count" => new UpdateSettingsRequest { Count = int.Parse(value, CultureInfo.InvariantCulture) },
            "timezone" => new UpdateSettingsRequest { Timezone = value },
            _ => new UpdateSettingsRequest()
        };
    }

    private void RebuildFields()
    {
        _fields.Clear();

        if (_settings is null)
            return;

        _fields.Add(new SettingsField("Kindle Email", _settings.KindleEmail, "kindleEmail", FieldKind.Editable));
        _fields.Add(new SettingsField("Schedule", _settings.Schedule, "schedule", FieldKind.Editable));
        _fields.Add(new SettingsField("Delivery Time", _settings.DeliveryTime, "deliveryTime", FieldKind.Editable));
        _fields.Add(new SettingsField("Timezone", _settings.Timezone, "timezone", FieldKind.Editable));
        _fields.Add(new SettingsField("Highlights per Recap", _settings.Count.ToString(CultureInfo.InvariantCulture), "count", FieldKind.Editable));
        _fields.Add(new SettingsField("▶ Send test email", string.Empty, "test-email", FieldKind.Action));

        if (_isDevelopment)
        {
            _fields.Add(new SettingsField("▶ Trigger recap (dev)", string.Empty, "trigger-recap", FieldKind.Action));
        }

        if (_selectedField >= _fields.Count)
            _selectedField = Math.Max(0, _fields.Count - 1);
    }

    private void SetStatus(string message, bool isError)
    {
        _statusMessage = message;
        _statusIsError = isError;
        UpdateViewStateIfCreated();
    }

    private void UpdateViewStateIfCreated()
    {
        if (_viewCreated)
            UpdateViewState();
    }

    private void UpdateViewState()
    {
        if (_fieldRows is not null)
        {
            _fieldRows.Clear();
            foreach (var field in _fields)
            {
                _fieldRows.Add(FormatField(field));
            }
        }

        if (_fieldList is not null && _fields.Count > 0)
        {
            _fieldList.SelectedItem = Math.Clamp(_selectedField, 0, _fields.Count - 1);
        }

        if (_statusLabel is not null)
        {
            if (_statusMessage is not null)
            {
                _statusLabel.Text = _statusMessage;
                _statusLabel.Visible = true;
                var color = _statusIsError ? new Color(255, 100, 100) : new Color(100, 220, 100);
                _statusLabel.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(color, StatusChrome.Background)));
            }
            else
            {
                _statusLabel.Visible = false;
            }
        }
    }

    private static string FormatField(SettingsField field)
    {
        if (field.Kind == FieldKind.Action)
            return $"  {field.Label}";

        var label = field.Label.PadRight(LabelColumnWidth);
        return $"  {label}{field.Value}";
    }

    private async Task HandleContainerKeyDownAsync(Key key)
    {
        if (_isEditing)
            return;

        if (!TryMapGlobalKey(key, out var mappedKey))
            return;

        key.Handled = true;
        var result = await HandleKeyAsync(mappedKey, CancellationToken.None).ConfigureAwait(false);
        ApplyNavigation(result);
    }

    private async Task HandleListKeyDownAsync(Key key)
    {
        if (_isEditing)
            return;

        if (!TryMapGlobalKey(key, out var mappedKey))
            return;

        key.Handled = true;
        var result = await HandleKeyAsync(mappedKey, CancellationToken.None).ConfigureAwait(false);
        ApplyNavigation(result);
    }

    private async Task HandleShortcutKeyAsync(Key key)
    {
        if (_isEditing)
            return;

        if (!TryMapGlobalKey(key, out var mappedKey))
            return;

        key.Handled = true;
        var result = await HandleKeyAsync(mappedKey, CancellationToken.None).ConfigureAwait(false);
        ApplyNavigation(result);
    }

    private async Task HandleFieldEnterAsync()
    {
        var result = await HandleKeyAsync(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false), CancellationToken.None).ConfigureAwait(false);
        ApplyNavigation(result);
    }

    private static bool TryMapGlobalKey(Key key, out ConsoleKeyInfo mapped)
    {
        mapped = default;
        var rune = key.AsRune.Value;

        if (key.KeyCode == KeyCode.Esc)
        {
            mapped = new ConsoleKeyInfo('\x1B', ConsoleKey.Escape, false, false, false);
            return true;
        }

        if (key.KeyCode == (KeyCode.C | KeyCode.CtrlMask))
        {
            mapped = new ConsoleKeyInfo('\x03', ConsoleKey.C, false, false, true);
            return true;
        }

        if (rune is 'q' or 'Q')
        {
            mapped = new ConsoleKeyInfo('q', ConsoleKey.Q, false, false, false);
            return true;
        }

        if (rune is 't' or 'T')
        {
            mapped = new ConsoleKeyInfo('t', ConsoleKey.T, false, false, false);
            return true;
        }

        if (rune is 'r' or 'R')
        {
            mapped = new ConsoleKeyInfo('r', ConsoleKey.R, false, false, false);
            return true;
        }

        return false;
    }

    private void ApplyNavigation(ScreenResult result)
    {
        if (result.Action != ScreenAction.None)
            _navigate?.Invoke(result);
    }

    public sealed record SettingsField(string Label, string Value, string FieldId, FieldKind Kind)
    {
        public string? ActionId => Kind == FieldKind.Action ? FieldId : null;
    }

    public enum FieldKind
    {
        Editable,
        Action
    }
}
