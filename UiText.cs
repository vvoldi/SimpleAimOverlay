using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace SimpleAimOverlay;

internal static class UiText
{
    internal static string Language { get; set; } = "en";
    private sealed class Caption { public string English=""; public bool Updating; }
    private static readonly ConditionalWeakTable<Control,Caption> captions=new();
    private static readonly Dictionary<string,string> Ukrainian=new(StringComparer.Ordinal) {
        ["Crosshair"]="Приціл", ["Editor"]="Редактор", ["Settings"]="Налаштування",
        ["Build a clean, readable crosshair"]="Налаштуй чіткий та зручний приціл", ["LIVE PREVIEW"]="ПОПЕРЕДНІЙ ПЕРЕГЛЯД",
        ["Style"]="Стиль", ["Color"]="Колір", ["Aim color"]="Колір прицілу", ["Opacity"]="Непрозорість",
        ["Size"]="Розмір", ["Image zoom"]="Масштаб малюнка", ["Thickness"]="Товщина", ["Gap"]="Проміжок",
        ["Outline"]="Обведення", ["Show outline"]="Показувати контур", ["Outline color"]="Колір контуру",
        ["Outline width"]="Товщина контуру", ["Outline opacity"]="Непрозорість контуру", ["Center offset"]="Зсув від центра",
        ["Display"]="Дисплей", ["Refresh displays"]="Оновити список дисплеїв", ["Primary"]="Основний",
        ["Cross"]="Хрест", ["Dot"]="Крапка", ["Circle"]="Коло", ["Cross + Dot"]="Хрест із крапкою",
        ["T-shape"]="Т-подібний", ["Diagonal"]="Діагональний", ["Chevron"]="Кут", ["3-ray"]="Три промені",
        ["Square"]="Квадрат", ["Predator"]="Хижак", ["Image"]="Малюнок",
        ["Your drawings. Create a crosshair or edit one you saved. Standard styles are in Crosshair."]="Створюй та редагуй власні малюнки. Стандартні стилі — у вкладці «Приціл».",
        ["New"]="Створити", ["Edit"]="Редагувати", ["Delete"]="Видалити", ["Import"]="Імпорт", ["Export"]="Експорт",
        ["Visibility"]="Видимість", ["Show crosshair"]="Показувати приціл", ["Toggle hotkey"]="Перемикання",
        ["Hold to hide"]="Тимчасово сховати", ["Hide while key is held"]="Ховати під час утримання клавіш", ["Hold key"]="Клавіші утримання",
        ["Startup"]="Автозапуск", ["Launch when Windows starts"]="Запускати разом із Windows",
        ["Close button"]="Кнопка закриття", ["Minimize to tray when closed"]="Згортати в трей замість закриття",
        ["Minimize button"]="Кнопка згортання", ["Minimize to tray with the − button"]="Кнопка «−» згортає в трей",
        ["Selected game"]="Вибрана гра", ["Show only in the selected game"]="Показувати лише у вибраній грі",
        ["Game EXE"]="Файл гри", ["Browse…"]="Обрати…", ["Game startup"]="Запуск із грою",
        ["Updates"]="Оновлення", ["Check for updates"]="Перевірити оновлення", ["Configuration"]="Конфігурація",
        ["Diagnostics"]="Діагностика", ["Open update logs"]="Відкрити журнали оновлень", ["Language"]="Мова",
        ["UIAccess overlay. Fullscreen support depends on the game and its graphics mode."]="Оверлей UIAccess. Підтримка повноекранного режиму залежить від гри та її графічного режиму.",
        ["Checks once at launch; manual checks are always available."]="Перевірка один раз при запуску або вручну.",
        ["Crosshair: ON"]="Приціл: УВІМК", ["Crosshair: OFF"]="Приціл: ВИМК",
        ["Open"]="Відкрити", ["Show / hide"]="Показати / сховати", ["Exit"]="Вийти",
        ["Right Mouse"]="ПКМ", ["Left Mouse"]="ЛКМ", ["Middle Mouse"]="Середня кнопка миші",
        ["Mouse X1"]="Кнопка миші X1", ["Mouse X2"]="Кнопка миші X2", ["Space"]="Пробіл",
        ["New crosshair"]="Новий приціл", ["Crosshair editor"]="Редактор прицілу", ["Save crosshair"]="Зберегти приціл",
        ["Name"]="Назва", ["Unsaved drawing"]="Незбережений малюнок", ["Save your changes before closing?"]="Зберегти зміни перед закриттям?",
        ["Save"]="Зберегти", ["Cancel"]="Скасувати", ["Discard"]="Не зберігати", ["Continue"]="Продовжити",
        ["Undo"]="Скасувати", ["Redo"]="Повторити", ["Clear"]="Очистити", ["Pen color"]="Колір пензля",
        ["Brush"]="Пензель", ["Alpha"]="Альфа", ["Eraser"]="Гумка", ["Use crosshair color"]="Колір прицілу",
        ["Canvas"]="Полотно", ["Pencil"]="Олівець", ["Line"]="Лінія", ["Rectangle"]="Прямокутник",
        ["Ellipse"]="Еліпс", ["Fill"]="Заливка", ["Eyedropper"]="Піпетка", ["Mirror X"]="Симетрія X", ["Mirror Y"]="Симетрія Y",
        ["Save drawing"]="Зберегти малюнок",
        ["PREVIEW · 1× / 4×\nRight mouse: erase\nCtrl+wheel: zoom\nCtrl+Z / Ctrl+Y: undo / redo"]="ПЕРЕГЛЯД · 1× / 4×\nПКМ: стирання\nCtrl+колесо: масштаб\nCtrl+Z / Ctrl+Y: скасувати / повторити",
        ["Choose color"]="Обрати колір", ["Current"]="Поточний", ["Original"]="Початковий", ["Apply"]="Застосувати",
        ["Set key binding"]="Призначити клавіші",
        ["Press up to 3 keys together. For mouse buttons, click below.\nClick Save to use the new binding."]="Натисни до 3 клавіш разом. Для кнопок миші — клацни нижче.\nНатисни «Зберегти», щоб застосувати комбінацію.",
        ["Use no more than 3 keys."]="Використовуй не більше 3 клавіш.",
        ["Select a crosshair first."]="Спочатку обери приціл.", ["Import drawing"]="Імпорт малюнка", ["Select game executable"]="Обрати виконуваний файл гри",
        ["Import failed"]="Помилка імпорту", ["Export failed"]="Помилка експорту", ["Drawing is too large."]="Малюнок завеликий.",
        ["Invalid drawing."]="Некоректний файл малюнка.", ["Drawings must be at most 1024 × 1024 pixels."]="Розмір малюнка не має перевищувати 1024 × 1024 пікселі.",
        ["Configuration is too large."]="Файл конфігурації завеликий.", ["Invalid configuration."]="Некоректна конфігурація.",
        ["Unsupported configuration format."]="Цей формат конфігурації не підтримується.", ["Invalid thickness."]="Некоректна товщина.", ["Invalid key binding."]="Некоректна комбінація клавіш.",
        ["Could not update the startup setting."]="Не вдалося змінити налаштування автозапуску.",
        ["Choose an existing game EXE, or enter its process filename (for example TslGame.exe)."]="Обери наявний EXE-файл гри або введи назву процесу, наприклад TslGame.exe.",
        ["Checking for updates…"]="Перевіряю оновлення…", ["No compatible update package is available."]="Сумісних оновлень поки немає.",
        ["Update available"]="Доступне оновлення", ["Connecting to GitHub…"]="Підключення до GitHub…",
        ["Preparing your update"]="Підготовка оновлення", ["Updating Simple Aim Overlay"]="Оновлення Simple Aim Overlay",
        ["Update and restart"]="Оновити й перезапустити", ["Not now"]="Не зараз",
        ["Ready to update"]="Готово до оновлення",
        ["Download verified. Update and restart when you are ready. Your settings will be kept."]="Файл перевірено. Підтвердь оновлення й перезапуск, коли будеш готовий. Налаштування буде збережено.",
        ["Please wait. The app will restart automatically."]="Зачекай. Програма перезапуститься автоматично.",
        ["Closing the app…"]="Закриття програми…", ["Preparing installation…"]="Підготовка встановлення…",
        ["Installing application files…"]="Встановлення файлів програми…", ["Updating shortcuts…"]="Оновлення ярликів…",
        ["Installation complete. Restarting…"]="Встановлення завершено. Перезапуск…",
        ["Cancelling…"]="Скасування…", ["Verifying update…"]="Перевірка файлу оновлення…",
        ["Waiting for Windows approval…"]="Очікування підтвердження Windows…", ["Keep the app open. Your settings will be preserved."]="Не закривай програму. Налаштування буде збережено.",
        ["Update check timed out or was cancelled."]="Час очікування вичерпано або перевірку скасовано.",
        ["Update cancelled. You can try again in Settings."]="Оновлення скасовано. Повторити можна в налаштуваннях.",
        ["Update unavailable · try again in Settings"]="Оновлення недоступне · повтори в налаштуваннях", ["Update could not be completed"]="Не вдалося завершити оновлення",
        ["The download is incomplete."]="Файл завантажено не повністю.", ["Update checksum verification failed."]="Контрольна сума оновлення не збігається.",
        ["Downloaded package size does not match the release."]="Розмір завантаженого файлу не збігається з релізом.",
        ["The release package metadata is invalid. Please use the GitHub release page."]="Некоректні дані релізу. Скористайся сторінкою релізів GitHub.",
        ["Could not start the installer."]="Не вдалося запустити інсталятор.", ["Could not verify the update signature."]="Не вдалося перевірити підпис оновлення."
        ,["My crosshair"]="Мій приціл", ["Import configuration"]="Імпорт конфігурації", ["Export configuration"]="Експорт конфігурації",
        ["Executable|*.exe"]="Виконувані файли|*.exe", ["Crosshair drawing|*.aim;*.png"]="Малюнки прицілів|*.aim;*.png",
        ["Shareable crosshair|*.aim|PNG image|*.png"]="Файл прицілу|*.aim|Зображення PNG|*.png", ["Aim Overlay configuration|*.aimconfig"]="Конфігурація Aim Overlay|*.aimconfig",
        ["Installation is complete. Launch Simple Aim Overlay now?"]="Встановлення завершено. Запустити Simple Aim Overlay зараз?",
        ["Remove the app and its test certificate from this PC?"]="Видалити програму та її тестовий сертифікат із цього ПК?",
        ["The app and its test certificate have been removed. The installation folder will be cleaned after Windows restarts."]="Програму та її тестовий сертифікат видалено. Папку встановлення буде очищено після перезапуску Windows."
    };
    internal static string T(string text)
    {
        if(Language!="uk")return text;
        if(Ukrainian.TryGetValue(text,out var value))return value;
        if(text.StartsWith("Display "))return Regex.Replace(text,@"^Display (\d+)","Дисплей $1").Replace(" · Primary"," · Основний");
        if(text.StartsWith("Version ")&&!text.Contains('\n'))return "Версія "+text[8..];
        if(text.StartsWith("You're up to date · "))return "Встановлена остання версія · "+text["You're up to date · ".Length..];
        if(text.StartsWith("Update available · "))return "Доступне оновлення · "+text["Update available · ".Length..];
        if(text.StartsWith("Downloading update"))return text.Replace("Downloading update","Завантаження оновлення");
        if(text.StartsWith("Crosshair hidden · switch to "))return "Приціл прихований · перейди до "+text["Crosshair hidden · switch to ".Length..];
        if(text.Contains(" toggles · ")||text.Contains(" held · crosshair hidden")||text.StartsWith("Crosshair off · ")) {
            text=text.Replace(" toggles · "," перемикає · ").Replace(" temporarily hides"," тимчасово ховає").Replace(" held · crosshair hidden"," утримується · приціл прихований").Replace("Crosshair off · ","Приціл вимкнено · ").Replace(" toggles visibility"," перемикає видимість");
            return text.Replace("Right Mouse","ПКМ").Replace("Left Mouse","ЛКМ").Replace("Middle Mouse","Середня кнопка миші");
        }
        if(text.Contains(" MB/s (last 4s)"))return text.Replace(" MB/s (last 4s)"," МБ/с (останні 4 с)").Replace(" MB"," МБ").Replace(" · about "," · залишилося приблизно ").Replace(" remaining","");
        if(text.Contains(" is available. Download it now?"))return Regex.Replace(text,@"^Version (.+?) is available\. Download it now\?", "Доступна версія $1. Завантажити?").Replace("You will confirm installation after the download. Your settings will be kept.","Після завантаження ти зможеш підтвердити встановлення. Налаштування буде збережено.");
        if(text.StartsWith("This is a self-signed Simple Aim Overlay "))return Regex.Replace(text,@"^This is a self-signed Simple Aim Overlay ([^.]+\.[^.]+\.[^ ]+) build\.","Це збірка Simple Aim Overlay $1 із самопідписаним сертифікатом.")
            .Replace("Setup will add the SimpleAimOverlay test certificate to this PC's trusted certificate stores and install the app in Program Files (required for UIAccess).","Інсталятор додасть тестовий сертифікат SimpleAimOverlay до довірених сховищ цього ПК та встановить програму в Program Files (потрібно для UIAccess).")
            .Replace("Only continue if you trust the package source. This build does not include a DXGI hook.","Продовжуй лише якщо довіряєш джерелу інсталятора. Ця збірка не містить DXGI hook.")
            .Replace("Continue? Windows will ask for administrator approval.","Продовжити? Windows запросить підтвердження адміністратора.");
        if(text.StartsWith("The operation could not be completed."))return text.Replace("The operation could not be completed.","Не вдалося завершити операцію.");
        if(text.Contains(" + "))return string.Join(" + ",text.Split(" + ").Select(T));
        return text;
    }
    internal static void Apply(Control root)
    {
        // Never translate user-entered text or internal combo/key-binding values.
        if((root is Label or ButtonBase or Form)&&root.GetType().Name!="KeyBindingButton") {
            if(!captions.TryGetValue(root,out var caption)) {
                caption=new Caption{English=root.Text};captions.Add(root,caption);
                root.TextChanged+=(_,_)=>{if(caption.Updating)return;caption.English=root.Text;Translate(root,caption);};
            }
            Translate(root,caption);
            if(root.GetType().Name=="NavigationButton")root.Width=Math.Max(126,TextRenderer.MeasureText(root.Text,root.Font).Width+48);
        }
        foreach(Control child in root.Controls)Apply(child);
        root.Invalidate();
    }
    private static void Translate(Control control,Caption caption){caption.Updating=true;try{control.Text=T(caption.English);}finally{caption.Updating=false;}}
}
