using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace SuperMacroV1;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, @"Local\SuperMacroV1.SingleInstance", out bool firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("SuperMacroV1 est déjà ouvert.", "SuperMacroV1",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class AppSettings
{
    public int Cps { get; set; } = 12;
    public int MouseButton { get; set; }
    public int ActivationMode { get; set; }
    public string HotKey { get; set; } = "F6";
    public bool RequireCtrl { get; set; }
    public bool RequireAlt { get; set; }
    public bool RequireShift { get; set; }
    public bool StartMacroOnLaunch { get; set; }
    public bool GamesOnly { get; set; }
    public int BloxPreset { get; set; } = 1;
    public int BloxDelayMs { get; set; } = 70;
    public string BloxHotKey { get; set; } = "F8";
    public bool BloxOnlyWhenRoblox { get; set; } = true;
}

internal static class SettingsStore
{
    private static readonly string DirectoryPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SuperMacroV1");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new AppSettings();

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath))
                   ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings,
            new JsonSerializerOptions { WriteIndented = true }));
    }
}

internal sealed class RuntimeConfig
{
    public int MouseButton { get; init; }
    public bool GamesOnly { get; init; }
}

internal sealed class MainForm : Form
{
    private readonly NumericUpDown _cps = new();
    private readonly ComboBox _mouseButton = new();
    private readonly ComboBox _mode = new();
    private readonly ComboBox _hotKey = new();
    private readonly CheckBox _ctrl = new();
    private readonly CheckBox _alt = new();
    private readonly CheckBox _shift = new();
    private readonly CheckBox _autoStart = new();
    private readonly CheckBox _gamesOnly = new();
    private readonly Label _status = new();
    private readonly Button _toggleButton = new();
    private readonly System.Windows.Forms.Timer _keyTimer = new();
    private readonly System.Threading.Timer _clickTimer;

    private volatile bool _active;
    private volatile RuntimeConfig _runtime = new();
    private bool _lastHotKeyDown;
    private bool _lastEmergencyDown;
    private bool _startupLatch;
    private AppSettings _settings;

    private static readonly Color Background = Color.FromArgb(18, 20, 26);
    private static readonly Color Panel = Color.FromArgb(29, 33, 42);
    private static readonly Color Accent = Color.FromArgb(117, 92, 255);
    private static readonly Color Foreground = Color.FromArgb(238, 240, 246);
    private static readonly Color Muted = Color.FromArgb(157, 164, 181);

    public MainForm()
    {
        _settings = SettingsStore.Load();

        Text = "SuperMacroV1";
        ClientSize = new Size(570, 610);
        MinimumSize = new Size(586, 649);
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Background;
        ForeColor = Foreground;
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildInterface();
        LoadSettingsIntoControls();

        _clickTimer = new System.Threading.Timer(ClickTick, null,
            Timeout.Infinite, Timeout.Infinite);

        WireEvents();
        ApplyRuntimeSettings();

        _keyTimer.Interval = 15;
        _keyTimer.Tick += PollKeys;
        _keyTimer.Start();

        Shown += (_, _) =>
        {
            if (_settings.StartMacroOnLaunch)
            {
                _startupLatch = _settings.ActivationMode == 1;
                SetActive(true);
            }
        };

        FormClosing += (_, _) =>
        {
            _keyTimer.Stop();
            _clickTimer.Dispose();
            SaveCurrentSettings();
        };
    }

    private void BuildInterface()
    {
        var logo = new PictureBox
        {
            Location = new Point(27, 18),
            Size = new Size(70, 70),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        using (Stream? logoStream = Assembly.GetExecutingAssembly()
                   .GetManifestResourceStream("SuperMacroV1.DistroStudios.png"))
        {
            if (logoStream is not null)
                logo.Image = new Bitmap(logoStream);
        }
        Controls.Add(logo);

        var title = new Label
        {
            Text = "SuperMacroV1",
            Font = new Font("Segoe UI Semibold", 24F),
            ForeColor = Foreground,
            AutoSize = true,
            Location = new Point(110, 18)
        };
        Controls.Add(title);

        var subtitle = new Label
        {
            Text = "DistroStudios • Tous clients Minecraft • Roblox",
            ForeColor = Muted,
            AutoSize = true,
            Location = new Point(113, 63)
        };
        Controls.Add(subtitle);

        var card = new Panel
        {
            BackColor = Panel,
            Location = new Point(25, 105),
            Size = new Size(520, 390)
        };
        Controls.Add(card);

        AddLabel(card, "Vitesse de clic", 24, 24);
        _cps.Location = new Point(270, 20);
        _cps.Size = new Size(210, 30);
        _cps.Minimum = 1;
        _cps.Maximum = 100;
        _cps.TextAlign = HorizontalAlignment.Center;
        card.Controls.Add(_cps);
        AddSmallLabel(card, "clics par seconde (CPS)", 274, 52);

        AddLabel(card, "Bouton de souris", 24, 86);
        SetupCombo(_mouseButton, 270, 81, new object[] { "Clic gauche", "Clic droit", "Clic milieu" });
        card.Controls.Add(_mouseButton);

        AddLabel(card, "Mode d'activation", 24, 136);
        SetupCombo(_mode, 270, 131, new object[] { "Bascule (appuyer une fois)", "Maintien de la touche" });
        card.Controls.Add(_mode);

        AddLabel(card, "Touche d'activation", 24, 186);
        SetupCombo(_hotKey, 270, 181, new object[]
        {
            "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11",
            "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
            "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
            "Insert", "Home", "End", "PageUp", "PageDown", "XButton1", "XButton2"
        });
        card.Controls.Add(_hotKey);

        _ctrl.Text = "Ctrl";
        _alt.Text = "Alt";
        _shift.Text = "Maj";
        SetupCheckBox(_ctrl, 270, 220);
        SetupCheckBox(_alt, 340, 220);
        SetupCheckBox(_shift, 405, 220);
        card.Controls.AddRange(new Control[] { _ctrl, _alt, _shift });

        _autoStart.Text = "Démarrer les clics à l'ouverture";
        SetupCheckBox(_autoStart, 24, 270);
        _autoStart.AutoSize = true;
        card.Controls.Add(_autoStart);

        _gamesOnly.Text = "Filtrer aux clients Minecraft/Roblox reconnus (décoché = universel)";
        SetupCheckBox(_gamesOnly, 24, 310);
        _gamesOnly.AutoSize = true;
        card.Controls.Add(_gamesOnly);

        var safety = new Label
        {
            Text = "F12 : arrêt d'urgence global",
            ForeColor = Color.FromArgb(255, 183, 77),
            AutoSize = true,
            Location = new Point(24, 352)
        };
        card.Controls.Add(safety);

        _toggleButton.Text = "DÉMARRER";
        _toggleButton.Location = new Point(25, 515);
        _toggleButton.Size = new Size(245, 55);
        _toggleButton.FlatStyle = FlatStyle.Flat;
        _toggleButton.FlatAppearance.BorderSize = 0;
        _toggleButton.BackColor = Accent;
        _toggleButton.ForeColor = Color.White;
        _toggleButton.Font = new Font("Segoe UI Semibold", 12F);
        _toggleButton.Cursor = Cursors.Hand;
        Controls.Add(_toggleButton);

        var saveButton = new Button
        {
            Text = "ENREGISTRER",
            Location = new Point(280, 515),
            Size = new Size(125, 55),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(48, 53, 66),
            ForeColor = Foreground,
            Font = new Font("Segoe UI Semibold", 10F),
            Cursor = Cursors.Hand
        };
        saveButton.FlatAppearance.BorderColor = Color.FromArgb(70, 76, 92);
        saveButton.Click += (_, _) =>
        {
            SaveCurrentSettings();
            MessageBox.Show("Paramètres enregistrés.", "SuperMacroV1",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        Controls.Add(saveButton);

        var bloxButton = new Button
        {
            Text = "BLOX FRUITS",
            Location = new Point(415, 515),
            Size = new Size(130, 55),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(214, 45, 142),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 9.5F),
            Cursor = Cursors.Hand
        };
        bloxButton.FlatAppearance.BorderSize = 0;
        bloxButton.Click += (_, _) =>
        {
            using var bloxForm = new BloxFruitsForm();
            bloxForm.ShowDialog(this);
            _settings = SettingsStore.Load();
        };
        Controls.Add(bloxButton);

        _status.Text = "INACTIF";
        _status.ForeColor = Muted;
        _status.AutoSize = true;
        _status.Font = new Font("Segoe UI Semibold", 10F);
        _status.Location = new Point(28, 582);
        Controls.Add(_status);
    }

    private static void AddLabel(Control parent, string text, int x, int y)
    {
        parent.Controls.Add(new Label
        {
            Text = text,
            ForeColor = Foreground,
            AutoSize = true,
            Location = new Point(x, y + 3)
        });
    }

    private static void AddSmallLabel(Control parent, string text, int x, int y)
    {
        parent.Controls.Add(new Label
        {
            Text = text,
            ForeColor = Muted,
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F),
            Location = new Point(x, y)
        });
    }

    private static void SetupCombo(ComboBox combo, int x, int y, object[] values)
    {
        combo.Location = new Point(x, y);
        combo.Size = new Size(210, 32);
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.FlatStyle = FlatStyle.Flat;
        combo.BackColor = Color.FromArgb(42, 47, 59);
        combo.ForeColor = Foreground;
        combo.Items.AddRange(values);
    }

    private static void SetupCheckBox(CheckBox checkBox, int x, int y)
    {
        checkBox.Location = new Point(x, y);
        checkBox.ForeColor = Foreground;
        checkBox.BackColor = Color.Transparent;
    }

    private void LoadSettingsIntoControls()
    {
        _cps.Value = Math.Clamp(_settings.Cps, 1, 100);
        _mouseButton.SelectedIndex = Math.Clamp(_settings.MouseButton, 0, 2);
        _mode.SelectedIndex = Math.Clamp(_settings.ActivationMode, 0, 1);
        _hotKey.SelectedItem = _settings.HotKey;
        if (_hotKey.SelectedIndex < 0)
            _hotKey.SelectedItem = "F6";

        _ctrl.Checked = _settings.RequireCtrl;
        _alt.Checked = _settings.RequireAlt;
        _shift.Checked = _settings.RequireShift;
        _autoStart.Checked = _settings.StartMacroOnLaunch;
        _gamesOnly.Checked = _settings.GamesOnly;
    }

    private void WireEvents()
    {
        _toggleButton.Click += (_, _) =>
        {
            _startupLatch = false;
            SetActive(!_active);
        };

        _cps.ValueChanged += (_, _) => ApplyRuntimeSettings();
        _mouseButton.SelectedIndexChanged += (_, _) => ApplyRuntimeSettings();
        _gamesOnly.CheckedChanged += (_, _) => ApplyRuntimeSettings();
    }

    private void ApplyRuntimeSettings()
    {
        _runtime = new RuntimeConfig
        {
            MouseButton = Math.Max(0, _mouseButton.SelectedIndex),
            GamesOnly = _gamesOnly.Checked
        };

        int interval = Math.Max(1, (int)Math.Round(1000.0 / (double)_cps.Value));
        if (_clickTimer is not null)
            _clickTimer.Change(0, interval);
    }

    private void PollKeys(object? sender, EventArgs e)
    {
        bool emergencyDown = NativeMethods.IsKeyDown(Keys.F12);
        if (emergencyDown && !_lastEmergencyDown)
        {
            _startupLatch = false;
            SetActive(false);
        }
        _lastEmergencyDown = emergencyDown;

        Keys key = Enum.TryParse(_hotKey.SelectedItem?.ToString(), out Keys parsed)
            ? parsed
            : Keys.F6;

        bool hotKeyDown =
            NativeMethods.IsKeyDown(key) &&
            (!_ctrl.Checked || NativeMethods.IsKeyDown(Keys.ControlKey)) &&
            (!_alt.Checked || NativeMethods.IsKeyDown(Keys.Menu)) &&
            (!_shift.Checked || NativeMethods.IsKeyDown(Keys.ShiftKey));

        if (_mode.SelectedIndex == 0)
        {
            if (hotKeyDown && !_lastHotKeyDown)
            {
                _startupLatch = false;
                SetActive(!_active);
            }
        }
        else
        {
            if (_startupLatch)
            {
                if (hotKeyDown)
                    _startupLatch = false;
            }
            else
            {
                SetActive(hotKeyDown);
            }
        }

        _lastHotKeyDown = hotKeyDown;
        RefreshStatus();
    }

    private void SetActive(bool value)
    {
        _active = value;
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        if (!_active)
        {
            _status.Text = "● INACTIF";
            _status.ForeColor = Muted;
            _toggleButton.Text = "DÉMARRER";
            _toggleButton.BackColor = Accent;
            return;
        }

        if (_runtime.GamesOnly && !NativeMethods.IsSupportedGameForeground())
        {
            _status.Text = "● ARMÉ — EN ATTENTE DE MINECRAFT/ROBLOX";
            _status.ForeColor = Color.FromArgb(255, 183, 77);
        }
        else
        {
            _status.Text = "● ACTIF";
            _status.ForeColor = Color.FromArgb(76, 217, 142);
        }

        _toggleButton.Text = "ARRÊTER";
        _toggleButton.BackColor = Color.FromArgb(214, 69, 79);
    }

    private void ClickTick(object? state)
    {
        if (!_active)
            return;

        RuntimeConfig config = _runtime;
        if (config.GamesOnly && !NativeMethods.IsSupportedGameForeground())
            return;

        NativeMethods.SendMouseClick(config.MouseButton);
    }

    private void SaveCurrentSettings()
    {
        _settings = new AppSettings
        {
            Cps = (int)_cps.Value,
            MouseButton = Math.Max(0, _mouseButton.SelectedIndex),
            ActivationMode = Math.Max(0, _mode.SelectedIndex),
            HotKey = _hotKey.SelectedItem?.ToString() ?? "F6",
            RequireCtrl = _ctrl.Checked,
            RequireAlt = _alt.Checked,
            RequireShift = _shift.Checked,
            StartMacroOnLaunch = _autoStart.Checked,
            GamesOnly = _gamesOnly.Checked,
            BloxPreset = _settings.BloxPreset,
            BloxDelayMs = _settings.BloxDelayMs,
            BloxHotKey = _settings.BloxHotKey,
            BloxOnlyWhenRoblox = _settings.BloxOnlyWhenRoblox
        };
        SettingsStore.Save(_settings);
    }
}

internal sealed class BloxFruitsForm : Form
{
    private readonly ComboBox _preset = new();
    private readonly NumericUpDown _delay = new();
    private readonly ComboBox _hotKey = new();
    private readonly CheckBox _robloxOnly = new();
    private readonly Label _status = new();
    private readonly System.Windows.Forms.Timer _keyTimer = new();
    private AppSettings _settings;
    private bool _lastHotKeyDown;
    private bool _lastEmergencyDown;
    private bool _running;
    private CancellationTokenSource? _cancellation;

    private static readonly Color Background = Color.FromArgb(18, 20, 26);
    private static readonly Color Panel = Color.FromArgb(29, 33, 42);
    private static readonly Color Foreground = Color.FromArgb(238, 240, 246);
    private static readonly Color Muted = Color.FromArgb(157, 164, 181);
    private static readonly Color Accent = Color.FromArgb(214, 45, 142);

    public BloxFruitsForm()
    {
        _settings = SettingsStore.Load();

        Text = "SuperMacroV1 — Blox Fruits";
        ClientSize = new Size(540, 455);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Background;
        ForeColor = Foreground;
        Font = new Font("Segoe UI", 10F);

        BuildInterface();
        LoadSettings();

        _keyTimer.Interval = 15;
        _keyTimer.Tick += PollHotKey;
        _keyTimer.Start();

        FormClosing += (_, _) =>
        {
            _cancellation?.Cancel();
            _keyTimer.Stop();
            SaveSettings();
        };
    }

    private void BuildInterface()
    {
        Controls.Add(new Label
        {
            Text = "Blox Fruits",
            Font = new Font("Segoe UI Semibold", 23F),
            ForeColor = Foreground,
            AutoSize = true,
            Location = new Point(25, 20)
        });

        Controls.Add(new Label
        {
            Text = "Macros de déplacement • touches entièrement réglables",
            ForeColor = Muted,
            AutoSize = true,
            Location = new Point(28, 64)
        });

        var card = new Panel
        {
            BackColor = Panel,
            Location = new Point(25, 98),
            Size = new Size(490, 265)
        };
        Controls.Add(card);

        AddLabel(card, "Preset", 22, 25);
        SetupCombo(_preset, 175, 20, new object[]
        {
            "Air Dash — Espace → Q",
            "Soru → Air Dash — R → Espace → Q",
            "Air Jump → Soru → Dash — Espace → R → Q"
        });
        _preset.Size = new Size(285, 32);
        card.Controls.Add(_preset);

        AddLabel(card, "Délai entre touches", 22, 85);
        _delay.Location = new Point(280, 80);
        _delay.Size = new Size(180, 30);
        _delay.Minimum = 20;
        _delay.Maximum = 500;
        _delay.Increment = 10;
        _delay.TextAlign = HorizontalAlignment.Center;
        card.Controls.Add(_delay);

        AddLabel(card, "Raccourci macro", 22, 140);
        SetupCombo(_hotKey, 280, 135, new object[]
        {
            "F1", "F2", "F3", "F4", "F5", "F6",
            "F7", "F8", "F9", "F10", "F11", "XButton1", "XButton2"
        });
        _hotKey.Size = new Size(180, 32);
        card.Controls.Add(_hotKey);

        _robloxOnly.Text = "Exécuter uniquement quand Roblox est au premier plan";
        _robloxOnly.AutoSize = true;
        _robloxOnly.Location = new Point(22, 190);
        _robloxOnly.ForeColor = Foreground;
        card.Controls.Add(_robloxOnly);

        card.Controls.Add(new Label
        {
            Text = "Maintiens une direction (W/A/S/D) pour orienter le dash. F12 annule.",
            ForeColor = Color.FromArgb(255, 183, 77),
            AutoSize = true,
            Location = new Point(22, 228)
        });

        var runButton = new Button
        {
            Text = "EXÉCUTER DANS 2 SECONDES",
            Location = new Point(25, 382),
            Size = new Size(320, 48),
            FlatStyle = FlatStyle.Flat,
            BackColor = Accent,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 10.5F),
            Cursor = Cursors.Hand
        };
        runButton.FlatAppearance.BorderSize = 0;
        runButton.Click += async (_, _) => await RunMacroAsync(true);
        Controls.Add(runButton);

        _status.Text = "PRÊT";
        _status.ForeColor = Muted;
        _status.AutoSize = true;
        _status.Font = new Font("Segoe UI Semibold", 10F);
        _status.Location = new Point(365, 397);
        Controls.Add(_status);
    }

    private static void AddLabel(Control parent, string text, int x, int y)
    {
        parent.Controls.Add(new Label
        {
            Text = text,
            ForeColor = Foreground,
            AutoSize = true,
            Location = new Point(x, y + 3)
        });
    }

    private static void SetupCombo(ComboBox combo, int x, int y, object[] values)
    {
        combo.Location = new Point(x, y);
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.FlatStyle = FlatStyle.Flat;
        combo.BackColor = Color.FromArgb(42, 47, 59);
        combo.ForeColor = Foreground;
        combo.Items.AddRange(values);
    }

    private void LoadSettings()
    {
        _preset.SelectedIndex = Math.Clamp(_settings.BloxPreset, 0, 2);
        _delay.Value = Math.Clamp(_settings.BloxDelayMs, 20, 500);
        _hotKey.SelectedItem = _settings.BloxHotKey;
        if (_hotKey.SelectedIndex < 0)
            _hotKey.SelectedItem = "F8";
        _robloxOnly.Checked = _settings.BloxOnlyWhenRoblox;
    }

    private void SaveSettings()
    {
        _settings.BloxPreset = Math.Max(0, _preset.SelectedIndex);
        _settings.BloxDelayMs = (int)_delay.Value;
        _settings.BloxHotKey = _hotKey.SelectedItem?.ToString() ?? "F8";
        _settings.BloxOnlyWhenRoblox = _robloxOnly.Checked;
        SettingsStore.Save(_settings);
    }

    private void PollHotKey(object? sender, EventArgs e)
    {
        bool emergency = NativeMethods.IsKeyDown(Keys.F12);
        if (emergency && !_lastEmergencyDown)
        {
            _cancellation?.Cancel();
            _status.Text = "ANNULÉ";
            _status.ForeColor = Color.FromArgb(214, 69, 79);
        }
        _lastEmergencyDown = emergency;

        Keys key = Enum.TryParse(_hotKey.SelectedItem?.ToString(), out Keys parsed)
            ? parsed
            : Keys.F8;
        bool down = NativeMethods.IsKeyDown(key);

        if (down && !_lastHotKeyDown && !_running)
            _ = RunMacroAsync(false);

        _lastHotKeyDown = down;
    }

    private async Task RunMacroAsync(bool countdown)
    {
        if (_running)
            return;

        _running = true;
        _cancellation = new CancellationTokenSource();
        CancellationToken token = _cancellation.Token;

        try
        {
            SaveSettings();

            if (countdown)
            {
                _status.Text = "2 SECONDES…";
                _status.ForeColor = Color.FromArgb(255, 183, 77);
                await Task.Delay(2000, token);
            }

            if (_robloxOnly.Checked && !NativeMethods.IsRobloxForeground())
            {
                _status.Text = "ROBLOX NON ACTIF";
                _status.ForeColor = Color.FromArgb(214, 69, 79);
                return;
            }

            Keys[] sequence = _preset.SelectedIndex switch
            {
                0 => new[] { Keys.Space, Keys.Q },
                2 => new[] { Keys.Space, Keys.R, Keys.Q },
                _ => new[] { Keys.R, Keys.Space, Keys.Q }
            };

            _status.Text = "EXÉCUTION";
            _status.ForeColor = Color.FromArgb(76, 217, 142);

            foreach (Keys key in sequence)
            {
                token.ThrowIfCancellationRequested();
                NativeMethods.SendKeyPress(key);
                await Task.Delay((int)_delay.Value, token);
            }

            _status.Text = "TERMINÉ";
            _status.ForeColor = Color.FromArgb(76, 217, 142);
        }
        catch (OperationCanceledException)
        {
            _status.Text = "ANNULÉ";
            _status.ForeColor = Color.FromArgb(214, 69, 79);
        }
        finally
        {
            _running = false;
            _cancellation.Dispose();
            _cancellation = null;
        }
    }
}

internal static class NativeMethods
{
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint KeyUp = 0x0002;
    private const uint LeftDown = 0x0002;
    private const uint LeftUp = 0x0004;
    private const uint RightDown = 0x0008;
    private const uint RightUp = 0x0010;
    private const uint MiddleDown = 0x0020;
    private const uint MiddleUp = 0x0040;

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint numberOfInputs, Input[] inputs, int sizeOfInput);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    public static bool IsKeyDown(Keys key)
    {
        return (GetAsyncKeyState((int)key) & 0x8000) != 0;
    }

    public static void SendMouseClick(int button)
    {
        uint down;
        uint up;

        switch (button)
        {
            case 1:
                down = RightDown;
                up = RightUp;
                break;
            case 2:
                down = MiddleDown;
                up = MiddleUp;
                break;
            default:
                down = LeftDown;
                up = LeftUp;
                break;
        }

        var inputs = new[]
        {
            NewMouseInput(down),
            NewMouseInput(up)
        };

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static Input NewMouseInput(uint flags)
    {
        return new Input
        {
            Type = InputMouse,
            Data = new InputUnion
            {
                Mouse = new MouseInput
                {
                    Flags = flags,
                    ExtraInfo = UIntPtr.Zero
                }
            }
        };
    }

    public static void SendKeyPress(Keys key)
    {
        var inputs = new[]
        {
            NewKeyboardInput(key, 0),
            NewKeyboardInput(key, KeyUp)
        };

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static Input NewKeyboardInput(Keys key, uint flags)
    {
        return new Input
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = (ushort)key,
                    Flags = flags,
                    ExtraInfo = UIntPtr.Zero
                }
            }
        };
    }

    private static readonly string[] SupportedProcessMarkers =
    {
        "minecraft", "java", "javaw", "lunar", "cmclient", "badlion",
        "feather", "labymod", "salwyrr", "prism", "multimc", "curseforge",
        "modrinth", "sklauncher", "tlauncher", "gdlauncher", "atlauncher",
        "technic", "ftb", "robloxplayerbeta"
    };

    private static readonly string[] SupportedTitleMarkers =
    {
        "minecraft", "lunar client", "cmclient", "badlion", "feather client",
        "labymod", "salwyrr", "prism launcher", "multimc", "curseforge",
        "modrinth", "fabric", "forge", "quilt", "optifine", "roblox"
    };

    public static bool IsSupportedGameForeground()
    {
        try
        {
            IntPtr window = GetForegroundWindow();
            if (window == IntPtr.Zero)
                return false;

            var titleBuilder = new StringBuilder(512);
            GetWindowText(window, titleBuilder, titleBuilder.Capacity);
            string title = titleBuilder.ToString().ToLowerInvariant();

            GetWindowThreadProcessId(window, out uint processId);
            string process = Process.GetProcessById((int)processId).ProcessName.ToLowerInvariant();

            return ContainsAny(process, SupportedProcessMarkers) ||
                   ContainsAny(title, SupportedTitleMarkers);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsRobloxForeground()
    {
        try
        {
            IntPtr window = GetForegroundWindow();
            if (window == IntPtr.Zero)
                return false;

            var titleBuilder = new StringBuilder(512);
            GetWindowText(window, titleBuilder, titleBuilder.Capacity);
            string title = titleBuilder.ToString();

            GetWindowThreadProcessId(window, out uint processId);
            string process = Process.GetProcessById((int)processId).ProcessName;

            return title.Contains("roblox", StringComparison.OrdinalIgnoreCase) ||
                   process.Contains("robloxplayerbeta", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool ContainsAny(string value, IEnumerable<string> markers)
    {
        foreach (string marker in markers)
        {
            if (value.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
