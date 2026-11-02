namespace SimpleAimOverlay;

internal static class SettingsHelp
{
    private static readonly Dictionary<string, (string En, string Uk)> Texts = new()
    {
        ["Style"] = ("Choose a built-in crosshair shape. Image uses a saved drawing from the Editor.", "Вибери стандартну форму прицілу. Image використовує збережений малюнок із редактора."),
        ["Color"] = ("Choose the crosshair color. Bright colors are easier to see against the game background.", "Вибери колір прицілу. Яскраві кольори легше помітити на фоні гри."),
        ["Opacity"] = ("Controls crosshair transparency. Use 100% for the most visible crosshair.", "Керує прозорістю прицілу. Для максимальної видимості вибери 100%."),
        ["Size"] = ("Adjust the overall size of a built-in crosshair.", "Змінює загальний розмір стандартного прицілу."),
        ["Image zoom"] = ("Scale a custom drawing. Available only for the Image style.", "Масштабує власний малюнок. Доступно лише для стилю Image."),
        ["Thickness"] = ("Adjust line thickness for built-in shapes. Fractional values allow finer tuning.", "Змінює товщину ліній стандартних форм. Дробові значення дають точніше налаштування."),
        ["Gap"] = ("Adjust the space between the crosshair arms at the center. Available for built-in shapes.", "Змінює проміжок між лініями в центрі прицілу. Доступно для стандартних форм."),
        ["Outline"] = ("Add an outline to help the crosshair stand out against light or busy backgrounds.", "Додає обведення, щоб приціл краще виділявся на світлому або строкатому фоні."),
        ["Outline color"] = ("Choose a contrasting color for the crosshair outline.", "Вибери контрастний колір обведення прицілу."),
        ["Outline width"] = ("Set how thick the outline is. Enable Show outline to see it.", "Задає товщину обведення. Щоб його побачити, увімкни показ обведення."),
        ["Outline opacity"] = ("Change outline transparency independently from its color.", "Змінює прозорість обведення незалежно від його кольору."),
        ["Center offset"] = ("Move the crosshair from screen center: positive X moves right, positive Y moves down. Zero centers it.", "Зсуває приціл від центру екрана: додатний X — праворуч, Y — униз. Нулі повертають у центр."),
        ["Display"] = ("Choose the monitor on which the overlay appears.", "Вибери монітор, на якому відображатиметься приціл."),
        ["Refresh displays"] = ("Refresh the monitor list after connecting a display or changing its settings.", "Оновлює список після підключення монітора або зміни його налаштувань."),
        ["Language"] = ("Choose the interface language. Changes apply immediately.", "Вибери мову інтерфейсу. Зміни застосовуються одразу."),
        ["Theme"] = ("Choose a dark or light interface. This does not change the crosshair color.", "Вибери темний або світлий інтерфейс. Колір прицілу не зміниться."),
        ["Visibility"] = ("Turn the crosshair on or off. Temporary hide and game restrictions still apply when enabled.", "Вмикає або вимикає приціл. Коли він увімкнений, тимчасове приховування й обмеження вибраною грою залишаються чинними."),
        ["Toggle hotkey"] = ("Click to record a key or combination of up to three keys. Press it to toggle visibility. Default: F8.", "Натисни, щоб записати клавішу або комбінацію до трьох клавіш. Вона перемикає видимість. Типово: F8."),
        ["Hold to hide"] = ("Temporarily hide the crosshair while the Hold key is pressed, for example while aiming through a scope.", "Тимчасово ховає приціл, поки утримується задана клавіша, наприклад під час прицілювання через оптику."),
        ["Hold key"] = ("Click to bind the keyboard or mouse button that temporarily hides the crosshair. Default: Right Mouse.", "Натисни, щоб призначити клавішу або кнопку миші для тимчасового приховування. Типово: ПКМ."),
        ["Override hold-to-hide"] = ("Keep the crosshair visible while the override key is held, even with the hide key pressed. Useful for shoulder aiming. F8 and game restrictions still apply.", "Показує приціл під час утримання пріоритетної клавіші, навіть із затиснутою клавішею приховування. Корисно для прицілювання від плеча. F8 та обмеження грою зберігаються."),
        ["Override key"] = ("Bind any key or mouse button, such as shoulder aim. It takes priority regardless of press order. Releasing it restores normal hold-to-hide.", "Признач будь-яку клавішу чи кнопку миші, наприклад прицілювання від плеча. Вона має пріоритет незалежно від порядку натискань. Після відпускання звичайне приховування відновлюється."),
        ["Startup"] = ("Start at Windows sign-in, minimized to the tray. Open settings from the tray icon.", "Запускає програму при вході у Windows, згорнутою в трей. Налаштування можна відкрити через іконку в треї."),
        ["Desktop"] = ("Create a desktop shortcut to open the app or bring its existing window forward.", "Створює ярлик на робочому столі для запуску програми або відкриття її вже запущеного вікна."),
        ["OBS"] = ("Share a transparent crosshair with a local OBS Browser source. This makes it visible alongside Game Capture. Keep the app running.", "Передає прозорий приціл у локальне браузерне джерело OBS, щоб він був видимий разом із захопленням гри. Програма має залишатися запущеною."),
        ["Browser source"] = ("Copy the URL into an OBS Browser source above Game Capture. Use the shown monitor resolution and match the source bounds to the game.", "Скопіюй адресу в браузерне джерело OBS над захопленням гри. Вкажи показану роздільність монітора та вирівняй межі джерела з грою."),
        ["OBS status"] = ("Shows the sharing status, source resolution and local URL. If OBS opened before sharing was enabled, refresh its Browser source.", "Показує стан передачі, роздільність джерела та локальну адресу. Якщо OBS відкрився раніше, ніж увімкнено передачу, онови його браузерне джерело."),
        ["Close button"] = ("The window's X button hides settings in the tray instead of quitting. Use Exit in the tray menu to fully close the app.", "Хрестик вікна ховає налаштування в трей замість завершення роботи. Для повного закриття вибери «Вийти» в меню трея."),
        ["Minimize button"] = ("The minus button minimizes to the tray instead of keeping a taskbar button.", "Кнопка мінус згортає програму в трей замість панелі завдань."),
        ["Selected game"] = ("Show the crosshair only while the selected game is active. Alt+Tab hides it. A background watcher also starts the app when that game launches.", "Показує приціл лише коли вибрана гра активна. Alt+Tab ховає його. Фоновий процес також запускає програму при запуску цієї гри."),
        ["Game EXE"] = ("Select the actual game executable, not its launcher. PUBG normally uses TslGame.exe.", "Вибери виконуваний файл самої гри, а не лаунчера. Для PUBG зазвичай це TslGame.exe."),
        ["Updates"] = ("Check GitHub for a newer version. Automatic checks run once per launch. You confirm installation after download and verification.", "Перевіряє нову версію на GitHub. Автоматична перевірка відбувається раз за запуск. Встановлення підтверджується після завантаження й перевірки."),
        ["Configuration"] = ("Export settings and drawings to a file for backup or transfer. Import replaces the current configuration.", "Експортує налаштування й малюнки у файл для резервної копії або перенесення. Імпорт замінює поточну конфігурацію."),
        ["Diagnostics"] = ("Open update download logs to investigate slow downloads or failed updates.", "Відкриває журнали завантаження оновлень для пошуку причин низької швидкості або помилок.")
    };
    internal static bool Has(string key) => Texts.ContainsKey(key);
    internal static string Get(string key) => Texts.TryGetValue(key, out var text) ? UiText.Language == "uk" ? text.Uk : text.En : "";
}

internal sealed class SettingHelpButton : Button
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Form, HelpTips> owners = new();
    private readonly string key;
    private HelpTips? tips;
    private bool hovered;
    internal SettingHelpButton(string key)
    {
        this.key = key; Text = "?"; Size = new Size(24,24); Margin = new Padding(0,10,3,0);
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Help;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        RefreshLanguage();
    }
    internal void RefreshLanguage() { AccessibleName = UiText.T(key); AccessibleDescription = SettingsHelp.Get(key); }
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e); hovered = true; Invalidate(); RefreshLanguage();
        if (FindForm() is { } owner) { tips = owners.GetValue(owner, form => new HelpTips(form)); tips.Attach(this); }
    }
    protected override void OnMouseLeave(EventArgs e) { tips?.Hide(this); hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { tips?.Hide(this); base.OnMouseDown(e); }
    protected override void OnVisibleChanged(EventArgs e) { if (!Visible) tips?.Hide(this); base.OnVisibleChanged(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? DarkTheme.Surface);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var bounds = new Rectangle(2,2,Width-5,Height-5);
        using var pen = new Pen(hovered || Focused ? DarkTheme.Text : DarkTheme.Muted, 1);
        e.Graphics.DrawEllipse(pen,bounds);
        TextRenderer.DrawText(e.Graphics,"?",Font,ClientRectangle,hovered ? DarkTheme.Text : DarkTheme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }
    protected override void Dispose(bool disposing) { if (disposing) tips?.Detach(this); base.Dispose(disposing); }

    private sealed class HelpTips
    {
        private readonly ToolTip tip = new() { InitialDelay = 400, ReshowDelay = 150, AutoPopDelay = 15000, ShowAlways = false };
        private Control? active;
        private bool disposed;
        internal HelpTips(Form owner) {
            owner.Deactivate += (_,_) => HideActive();
            owner.Disposed += (_,_) => { disposed = true; tip.Dispose(); active = null; };
        }
        internal void Attach(SettingHelpButton control) {
            if (disposed) return;
            HideActive(); active = control;
            // Native hover behavior only: clicking cannot pin a second tooltip.
            tip.SetToolTip(control, Wrap(control.AccessibleDescription ?? ""));
        }
        private static string Wrap(string text) {
            var result = new System.Text.StringBuilder(); int column = 0;
            foreach (string word in text.Split(' ')) {
                if (column + word.Length > 58) { result.AppendLine(); column = 0; }
                if (column > 0) { result.Append(' '); column++; }
                result.Append(word); column += word.Length;
            }
            return result.ToString();
        }
        internal void Hide(Control control) { if (active == control) HideActive(); }
        internal void Detach(Control control) { Hide(control); if (!disposed) tip.SetToolTip(control,null); }
        private void HideActive() {
            if (disposed || active == null) return;
            tip.Hide(active); tip.SetToolTip(active,null); active = null;
        }
    }
}
