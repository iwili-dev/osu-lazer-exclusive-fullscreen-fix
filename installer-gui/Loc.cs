using System.Collections.Generic;

namespace OsuLazerFix
{
    /// <summary>All user-visible texts in two languages: index 0 = English, index 1 = Russian.</summary>
    internal static class Loc
    {
        public static int Lang;

        public static string T(string key, params object[] args)
        {
            string[] pair;
            string s = map.TryGetValue(key, out pair) ? pair[Lang] : key;
            return args.Length > 0 ? string.Format(s, args) : s;
        }

        private static readonly Dictionary<string, string[]> map = new Dictionary<string, string[]>
        {
            ["title"] = new[] { "osu!lazer Fix by iwili", "osu!lazer Fix by iwili" },
            ["lbl.game"] = new[] { "Game", "Игра" },
            ["lbl.status"] = new[] { "Status", "Статус" },
            ["short.none"] = new[] { "Not installed", "Не установлен" },
            ["short.online"] = new[] { "Online installed", "Установлен Online" },
            ["short.offline"] = new[] { "Partial offline installed", "Установлен Partial offline" },
            ["short.unknown"] = new[] { "Unknown", "Неизвестно" },
            ["short.nogame"] = new[] { "Game not found", "Игра не найдена" },
            ["short.unsupported"] = new[] { "Unsupported version", "Версия не поддерживается" },
            ["search"] = new[] { "Search version", "Поиск версии" },
            ["ed.online"] = new[] { "Online", "Online" },
            ["ed.offline"] = new[] { "Partial offline", "Частичный оффлайн" },
            ["btn.install"] = new[] { "Install", "Установить" },
            ["btn.installed"] = new[] { "Installed", "Установлено" },
            ["btn.remove"] = new[] { "Remove", "Удалить" },
            ["btn.cancel"] = new[] { "Cancel", "Отмена" },
            ["btn.ok"] = new[] { "OK", "OK" },
            ["btnAdmin"] = new[] { "Run as administrator", "Запустить от администратора" },
            ["settings"] = new[] { "Settings", "Настройки" },
            ["about"] = new[] { "About", "О программе" },
            ["set.lang"] = new[] { "Language", "Язык" },
            ["set.auto"] = new[] { "Auto-update", "Автообновление" },
            ["set.check"] = new[] { "Check for updates", "Проверить обновления" },
            ["set.folder"] = new[] { "Game folder", "Папка игры" },
            ["about.note"] = new[] { "Made for osu!lazer", "Сделано для osu!lazer" },
            ["update.avail"] = new[] { "Update {0} available", "Доступно обновление {0}" },
            ["update.ready"] = new[] { "Updated to {0}. Restart to use it", "Обновлено до {0}. Перезапустите" },
            ["btn.update"] = new[] { "Update", "Обновить" },
            ["btn.restart"] = new[] { "Restart", "Перезапустить" },
            ["btn.later"] = new[] { "Later", "Позже" },
            ["card.download"] = new[] { "Will be downloaded when you install it", "Скачается при установке" },
            ["dl.title"] = new[] { "Downloading", "Загрузка" },
            ["dl.text"] = new[] { "Downloading the patch files... {0}%", "Скачиваю файлы патча... {0}%" },
            ["dl.fail"] = new[] { "Couldn't download the patch: {0}\n\nCheck your internet connection and try again. Nothing in the game folder was changed.", "Не удалось скачать патч: {0}\n\nПроверьте подключение к интернету и повторите. В папке игры ничего не изменено." },
            ["upd.downloading"] = new[] { "Downloading the new version... {0}%", "Скачиваю новую версию... {0}%" },
            ["upd.ready"] = new[] { "Version {0} is installed. Restart the program to use it.", "Версия {0} установлена. Перезапустите программу, чтобы начать ею пользоваться." },
            ["upd.noAccess"] = new[] { "The program can't replace itself in this folder (no access). Download the new version from the releases page, or move the program to another folder, for example Downloads or Desktop.", "Программа не может заменить себя в этой папке (нет доступа). Скачайте новую версию со страницы релизов или перенесите программу в другую папку, например «Загрузки» или «Рабочий стол»." },
            ["upd.failed"] = new[] { "Couldn't update the program: {0}\n\nYou can download the new version from the releases page.", "Не удалось обновить программу: {0}\n\nНовую версию можно скачать со страницы релизов." },
            ["err.hash"] = new[] { "The downloaded file {0} is damaged or was changed (checksum mismatch). It was not used.", "Скачанный файл {0} повреждён или изменён (контрольная сумма не совпадает). Он не использован." },
            ["err.notDownloaded"] = new[] { "The patch files are not downloaded. Try again with an internet connection.", "Файлы патча не скачаны. Повторите при подключении к интернету." },
            ["upd.title"] = new[] { "Updates", "Обновления" },
            ["upd.latest"] = new[] { "You have the latest version.", "У вас последняя версия." },
            ["upd.new"] = new[] { "A new version of the program is available: {0}. Update now? It takes a few seconds.", "Доступна новая версия программы: {0}. Обновить сейчас? Это займёт несколько секунд." },
            ["upd.fail"] = new[] { "Couldn't check for updates. Check your internet connection.", "Не удалось проверить обновления. Проверьте подключение к интернету." },
            ["upd.page"] = new[] { "Open page", "Открыть страницу" },
            ["disc.title"] = new[] { "Before you install", "Перед установкой" },
            ["disc.text"] = new[] { "This is not an official version of the game. I made this fix for myself, so if something breaks, that's on you, not me.\n\nI don't know if an account can get banned for modified game files. Maybe yes, maybe no. Use it at your own risk.", "Это неофициальная версия игры. Фикс я делал для себя, поэтому если что-то пойдёт не так, отвечаете вы, а не я.\n\nБанят ли аккаунт за изменённые файлы игры, я не знаю: может да, может нет. Всё на ваш страх и риск." },
            ["disc.offline"] = new[] { "In this edition only these work online: logging in, downloading maps in the game, map leaderboards and the list of online players. Multiplayer, ranked and score submission don't.", "В этой редакции онлайн работает только это: вход в аккаунт, скачивание карт в игре, топ на карте и список игроков онлайн. Мультиплеер, ранговая игра и отправка результатов не работают." },
            ["disc.ask"] = new[] { "Continue?", "Продолжить?" },
            ["confirmRevert.title"] = new[] { "Remove fix", "Удаление фикса" },
            ["confirmRevert"] = new[] { "Remove the fix and restore the original game files?", "Удалить фикс и вернуть оригинальные файлы игры?" },
            ["msg.error"] = new[] { "Error", "Ошибка" },
            ["msg.done"] = new[] { "Done", "Готово" },
            ["browseDesc"] = new[] { "Select the osu!lazer folder. It contains osu!.exe or a folder named current.", "Выберите папку osu!lazer: в ней лежит osu!.exe или папка current." },
            ["err.running"] = new[] { "osu!lazer is running. Close the game and try again.", "osu!lazer запущена. Закройте игру и повторите." },
            ["err.dirty"] = new[] { "The game folder already has modified files, but there is no backup of the originals. Reinstall osu!lazer with the official installer from osu.ppy.sh (your maps and settings stay) and try again.", "В папке игры уже лежат изменённые файлы, а резервной копии оригиналов нет. Переустановите osu!lazer официальным установщиком с osu.ppy.sh (карты и настройки сохранятся) и повторите." },
            ["err.access"] = new[] { "Access denied. If the game is in a protected folder (for example Program Files), run this program as administrator. The original files were put back.", "Нет доступа к файлам. Если игра лежит в защищённой папке (например, Program Files), запустите программу от имени администратора. Оригинальные файлы возвращены на место." },
            ["err.copy"] = new[] { "Couldn't write a file: {0}. The original files were put back.", "Не удалось записать файл: {0}. Оригинальные файлы возвращены на место." },
            ["err.noedition"] = new[] { "This edition isn't included in this build.", "Этой редакции нет в этой сборке." },
            ["err.revert"] = new[] { "Couldn't restore the files: {0}. The backup was kept.", "Не удалось вернуть файлы: {0}. Резервная копия сохранена." },
            ["log.gameNotFound"] = new[] { "osu!.exe was not found in that folder. Select the osu!lazer folder.", "В этой папке нет osu!.exe. Выберите папку osu!lazer." },
            ["log.done"] = new[] { "Done. The fix is installed. In the game: Settings - Graphics - Renderer: Direct3D 11, Layout mode: Fullscreen.", "Готово, фикс установлен. В игре: Settings - Graphics - Renderer: Direct3D 11, Layout mode: Fullscreen." },
            ["log.revertDone"] = new[] { "Done. The fix was removed.", "Готово, фикс удалён." },
            ["log.stale"] = new[] { "Found a backup from an older version of the fix. It is out of date.", "Найдена копия от старой версии фикса, она устарела." },
            ["log.staleRemoved"] = new[] { "Removed the old backup, a new one will be made.", "Старая копия удалена, будет создана новая." },
            ["log.backedUp"] = new[] { "Original saved: {0}", "Оригинал сохранён: {0}" },
            ["log.framework"] = new[] { "Official osu.Framework.dll put back.", "Официальный osu.Framework.dll возвращён." },
            ["log.layer"] = new[] { "Compatibility flag for osu!.exe set.", "Флаг совместимости для osu!.exe установлен." },
            ["log.copied"] = new[] { "Installed: {0}", "Установлено: {0}" },
            ["log.nothing"] = new[] { "No backup found, nothing to restore.", "Резервной копии нет, возвращать нечего." },
            ["log.restored"] = new[] { "Original files restored.", "Оригинальные файлы возвращены." },
            ["log.updatedSkip"] = new[] { "The game was updated after the fix was installed, its files were not touched.", "Игра обновилась после установки фикса, её файлы не тронуты." },
            ["log.layerRemoved"] = new[] { "Compatibility flag removed.", "Флаг совместимости удалён." },
        };
    }
}
