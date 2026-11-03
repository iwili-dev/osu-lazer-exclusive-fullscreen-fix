<div align="center">

# osu!lazer Exclusive Fullscreen Fix

**Настоящий эксклюзивный полноэкранный режим для osu!lazer на Direct3D 11. Убирает импут лаг.**

[English](README.md) · [Русский](README.ru.md)

<br>

<a href="https://github.com/iwili-dev/osu-lazer-exclusive-fullscreen-fix/releases/download/app-1.1.0/osu-lazer-fix.exe"><img src="https://img.shields.io/badge/Скачать-osu--lazer--fix.exe-ff7eb6?style=for-the-badge&logo=windows&logoColor=white" alt="Скачать osu-lazer-fix.exe"></a>

<sub>Windows 10 / 11 · без установки, дополнительные программы не нужны</sub>

<br>

[![Программа](https://img.shields.io/badge/программа-1.1.0-ffbad8?style=flat-square)](https://github.com/iwili-dev/osu-lazer-exclusive-fullscreen-fix/releases/tag/app-1.1.0)
[![Игра](https://img.shields.io/badge/osu!lazer-2026.1005.0-ff66aa?style=flat-square)](https://github.com/iwili-dev/osu-lazer-exclusive-fullscreen-fix/releases/tag/2026.1005.0-2)
![Рендерер](https://img.shields.io/badge/рендерер-Direct3D%2011-444?style=flat-square)
![Платформа](https://img.shields.io/badge/платформа-Windows-444?style=flat-square)

</div>

---

## Что делает фикс?

В osu!lazer режим «Fullscreen» на самом деле не эксклюзивный. Игра рисует в оконную flip-цепочку и ни разу не просит у Windows эксклюзивный полноэкранный режим, поэтому картинка всё равно идёт через композитор рабочего стола. Отсюда лишний импут лаг.

Фикс заставляет игру занимать настоящий DXGI-эксклюзив, когда в настройках выбран Fullscreen и Direct3D 11. Когда окно теряет фокус, игра выходит из эксклюзива и возвращается в него, когда вы возвращаетесь в игру, так что Alt+Tab работает (экран один раз моргает). Для стримеров с 2+ мониторами в настройках программы есть экспериментальный «Режим для двух мониторов»: игра остаётся в эксклюзиве, пока вы кликаете по OBS или чату на другом мониторе.

Фикс ставится поверх обычной игры заменой DLL. `osu!.exe` и `osu.Game.dll` не трогаются.

> [!WARNING]
> Это неофициальная версия игры. Все действия вы выполняете на свой страх и риск. Автор не несёт ответственности за аккаунты и устройства.

## Установка

1. **Скачайте** [`osu-lazer-fix.exe`](https://github.com/iwili-dev/osu-lazer-exclusive-fullscreen-fix/releases/download/app-1.1.0/osu-lazer-fix.exe).
2. **Закройте osu!lazer.**
3. **Запустите exe.** Файл не подписан, поэтому Windows SmartScreen может показать синее окно. Нажмите *Подробнее*, затем *Выполнить в любом случае*. По той же причине на файл могут ругаться некоторые антивирусы. Весь исходный код лежит в этом репозитории, а в [BUILDING.md](BUILDING.md) написано, как собрать exe самому.
4. **Верх окна.** Там написана текущая версия игры, которая установлена на вашем устройстве. Программа ищет игру в `%LOCALAPPDATA%\osulazer\current`. Если игра лежит в другом месте, укажите папку в настройках.
5. **Откройте вкладку с версией вашей игры** и выберите издание (см. таблицу ниже).
6. **Нажмите «Установить»** и подтвердите. Файлы скачаются, проверятся и скопируются. Когда статус станет зелёным, всё готово.
7. **В игре** поставьте Renderer `Direct3D 11` и Layout mode `Fullscreen`. Когда включится эксклюзив, экран моргнёт один раз.

## Издания

|  | **Online** | **Partial offline** |
|---|---|---|
| Заменяет | `ppy.Veldrid.dll` | `ppy.Veldrid.dll`, `osu.Framework.dll` |
| Лимит FPS | 1000 (как был) | поднят до 2000 |
| Онлайн | работает всё | вход в аккаунт, скачивание карт в игре, топ скоров на карте, список игроков онлайн |
| Недоступно | ничего | мультиплеер, ранкед, отправка скоров |

**Partial offline.** В этом издании серверы не принимают игру как официальный клиент, поэтому игра показывает «Realtime online functionality is not supported» и «SCORE WILL NOT BE SUBMITTED». Так и должно быть.

## Удаление фикса

В программе нажмите **Удалить**. Оригинальные файлы вернутся из бэкапа. Бэкап лежит в папке `osu-fix-backup` рядом с `current`.

## После обновления игры

Игра обновляется заменой всей своей папки, поэтому с каждым обновлением фикс пропадает. Пока патча для новой версии нет, игра просто работает как официальная.

Программа при каждом запуске читает список патчей из этого репозитория, и новая версия появляется новой вкладкой. Если остался старый бэкап, программа это заметит и заменит его свежим.

Сама программа тоже обновляется. С включённым автообновлением она скачивает новую версию, проверяет её и подменяет себя при следующем запуске.

## Возможные ошибки

<details>
<summary><b>Для моей версии игры нет вкладки</b></summary>

Патч для этой версии ещё не вышел. Загляните позже: список грузится из интернета. Без соединения программа берёт последнюю сохранённую копию.
</details>

<details>
<summary><b>Программа пишет, что файлы игры не оригинальные</b></summary>

Кто-то уже изменил `ppy.Veldrid.dll` или `osu.Framework.dll`. Переустановите игру и запустите программу снова.
</details>

<details>
<summary><b>«Отказано в доступе» при установке</b></summary>

Игра лежит в защищённой папке. Нажмите в программе «Перезапустить от администратора».
</details>

<details>
<summary><b>Эксклюзив не включается</b></summary>

Проверьте, что Renderer стоит Direct3D 11, а Layout mode стоит Fullscreen. OpenGL не поддерживается: у Windows нет для него способа запросить эксклюзив, всё решает драйвер.
</details>

<details>
<summary><b>Курсор лагает с графическим планшетом</b></summary>

Такое встречается на слабых устройствах. Перезайдите в игру, может понадобиться несколько перезаходов. Перед запуском игры должен быть включён OpenTabletDriver или другой драйвер для графического планшета. В противном случае используйте встроенный драйвер в самой игре.
</details>

<details>
<summary><b>Плохо работает Alt+Tab</b></summary>

Alt+Tab в эксклюзивном полноэкранном режиме работает плохо: это особенности самого режима ради максимальной производительности.
</details>

## Батники вместо exe

Если удобнее скрипты, в [релизе `2026.1005.0-2`](https://github.com/iwili-dev/osu-lazer-exclusive-fullscreen-fix/releases/tag/2026.1005.0-2) лежат два zip-архива, по одному на издание. Распакуйте нужный, `apply.bat` ставит фикс, `revert.bat` убирает. У exe и батников общий бэкап, так что они могут откатывать работу друг друга.

## Как это работает

Veldrid, графический слой под osu!framework, создаёт flip-цепочку и никогда не вызывает `SetFullscreenState`. На такой цепочке вызов падает с ошибкой `DXGI_ERROR_INVALID_CALL`. Фикс на время эксклюзива переключается на классическую bitblt-цепочку, а при выходе возвращает flip.

Патч — это три файла в Veldrid. Его можно прочитать как diff в форке: [iwili-dev/veldrid](https://github.com/iwili-dev/veldrid/tree/exclusive-fullscreen) (ветка `exclusive-fullscreen`). Издание Partial offline дополнительно меняет одну строку в osu!framework, лимит FPS.

Каждый скачанный файл сверяется с SHA-256 из манифеста [`update/manifest.json`](update/manifest.json). В папку игры программа может записать только `ppy.Veldrid.dll` и `osu.Framework.dll`.

Инструкция по сборке: [BUILDING.md](BUILDING.md).

## Ссылки

- [Релиз программы 1.1.0](https://github.com/iwili-dev/osu-lazer-exclusive-fullscreen-fix/releases/tag/app-1.1.0) (exe)
- [Релиз патча 2026.1005.0-2](https://github.com/iwili-dev/osu-lazer-exclusive-fullscreen-fix/releases/tag/2026.1005.0-2) (батники)
- [Форк Veldrid](https://github.com/iwili-dev/veldrid)

## Авторство и лицензии

Фикс сделал [iwili](https://github.com/iwili-dev).

Основан на изменённом [Veldrid](https://github.com/iwili-dev/veldrid) (MIT, Eric Mellino и участники, форк ppy). Издание Partial offline также содержит изменённый [osu!framework](https://github.com/ppy/osu-framework) (MIT, ppy Pty Ltd). Тексты лицензий лежат в [`licenses/`](licenses).

osu! — торговая марка ppy Pty Ltd. Проект не связан с ppy.

---

<div align="center">

### Поддержать автора

<a href="https://ko-fi.com/iwili"><img src="https://img.shields.io/badge/Поддержать_на-Ko--fi-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white" alt="Поддержать на Ko-fi"></a>

<sub>Спасибо, что пользуетесь.</sub>

</div>
