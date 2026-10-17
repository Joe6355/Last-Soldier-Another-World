# Происхождение материалов

Проверено 17.10.2026. Этот реестр фиксирует найденные источники и лицензии; отсутствие записи не означает установленного нарушения. После выбора владельцем замены материалов с неизвестными правами проверка ведётся по фактическим зависимостям релизных сцен и Resources, а не по всем неиспользуемым картинкам проекта.

## Подтверждённые источники

| Материал | Источник и условия | Подтверждение в проекте |
| --- | --- | --- |
| PT Serif Bold, заголовки интерфейса | ParaType, SIL Open Font License 1.1 | `Assets/UI/ForestGarrison/Fonts/OFL.txt`; копия для сборки — `Assets/StreamingAssets/ThirdParty/PTSerif-OFL.txt` |
| Liberation Sans, стандартный шрифт TextMesh Pro | Google / Red Hat, SIL Open Font License 1.1 | `Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`; идентичная копия — `Assets/StreamingAssets/ThirdParty/LiberationSans-OFL.txt` |
| Plugin Your Games 2 | [Версия 2.0092](https://github.com/JustPlay-Max/Unity-PluginYG-2/releases/tag/2.0092), CC0 1.0 | `Assets/PluginYourGames/LICENSE.txt`; отдельные сторонние компоненты описаны в `Third-Party Notices.txt` |
| Summer Pixel Art Backgrounds | [Страница набора Craftpix](https://craftpix.net/freebies/free-summer-pixel-art-backgrounds/), бесплатный набор | `Assets/Sprits/ui/craftpix-net-256745-free-summer-pixel-art-backgrounds/License.txt` ссылается на условия Craftpix |
| Nature Backgrounds Pixel Art | [Страница набора Craftpix](https://craftpix.net/freebies/free-nature-backgrounds-pixel-art/), бесплатный набор | `Assets/Sprits/ui/craftpix-net-823949-free-nature-backgrounds-pixel-art/License.txt` ссылается на условия Craftpix |

[Лицензия Craftpix, раздел 2](https://craftpix.net/file-licenses/) разрешает использование бесплатных наборов в личных и коммерческих проектах, изменение и распространение игр; указание автора не требуется. Отдельное распространение исходных картинок ограничено. OFL-тексты шрифтов следует сохранять при распространении вместе со шрифтами.

## Дополнительно установленные источники, 17.10.2026

Публичные оригиналы скачаны с авторских страниц OpenGameArt и сравнены с локальными файлами **побайтово**. Получено 65 точных совпадений, включая 46 кадров VeMe. Пары URL, SHA-256 исходника/локального файла и пути загрузок сохранены в `Publishing/asset-rights-inventory.json`; исходные загрузки — `Builds/AssetReplacements/sources/originals` и `sources/veme.zip`. Совпадение имени или визуальное сходство не использовались как достаточное подтверждение.

| Файлы игры | Автор / первоисточник | Применённая лицензия |
| --- | --- | --- |
| `Map/32x32_map_tile v3.1 [MARGINLESS].png` | Ivan Voirol, [Basic map 32x32](https://opengameart.org/content/basic-map-tile-32x32) | CC BY 3.0; указать автора, источник и лицензию |
| `Map/basictiles.png`, `Map/things.png` | Lanea Zimmerman (Sharm), [Tiny 16: Basic](https://opengameart.org/content/tiny-16-basic) | Из предложенных лицензий выбрана CC BY 3.0; автор отдельно отменяет anti-DRM clause |
| Шесть `Map/{chicken,cow,sheep}_{eat,walk}.png` | Daniel Eddeland (daneeklu), [LPC style farm animals](https://opengameart.org/content/lpc-style-farm-animals) | CC BY 3.0; имя Daniel Eddeland и ссылка на авторскую страницу |
| `LPCportrait33.png` | PlayCraft / ZeNeRIA29, публикация ZaPaper, [LPC Anime Portrait #2](https://opengameart.org/content/lpc-anime-portrait-2) | CC BY 3.0; обязательная подпись «Portrait graphics created by PlayCraft» сохранена в credits |
| `slime_monster_spritesheet.png` | Bonsaiheldin, [Slime monster 24x24](https://opengameart.org/content/slime-monster-24x24) | CC BY 4.0; автор и ссылка на исходник |
| Все 46 PNG в `Boss/BossSlime/MiniBoss` | PiXeRaT (pixelato), [VeMe](https://opengameart.org/content/veme); профиль подтверждён README архива | CC BY-SA 4.0; кадры оригинала не изменены; для переработок самой графики сохраняется эта лицензия |
| `achievements.png`, `Map/achievements.png`; четыре производные медали | Buch, [Medals](https://opengameart.org/node/16656) | CC0 1.0; источник исходного атласа закрыт, замена палитр описана в `MEDALS.md` |
| `portalRings1.png`, `portalRings2.png` | LetargicDev, [Portals](https://opengameart.org/content/portals) | CC0 1.0 |

Короткие подписи, ссылки обычным текстом и известные изменения подготовлены в `Assets/Resources/AssetCredits.txt`. Полные официальные тексты `CC-BY-3.0.txt`, `CC-BY-4.0.txt`, `CC-BY-SA-4.0.txt`, `CC0-1.0.txt` включены в `Assets/StreamingAssets/ThirdParty`. Получены из [официального репозитория Creative Commons](https://github.com/creativecommons/cc-legal-tools-data/tree/main/legacy/legalcode); SHA-256 и URL загрузок — `Builds/AssetReplacements/sources/license-downloads.json`. Доступность окна credits проверяется основным сценарием интерфейса.

## Источники для согласованной замены графики

Пользователь выбрал замену материалов, чьи права не удалось подтвердить. Для упаковки подготовлены оригиналы в `Builds/AssetReplacements/sources`, URL и SHA-256 — `sources/downloads.json`:

- [Tiny 16: Basic](https://opengameart.org/content/tiny-16-basic): `tiny16-basic-characters.png`, 192×128; Lanea Zimmerman (Sharm), CC BY 3.0.
- [Tiny 16: Expanded Character Sprites](https://opengameart.org/content/tiny-16-expanded-character-sprites): общий лист 800×800, male 96×160, skeleton 96×96; withthelove, Lanea Zimmerman (Sharm), Evert, mdkieran; CC BY 3.0. Автор просит сохранить Sharm и Evert в атрибуции.
- [Tiny 16: more character animations](https://opengameart.org/content/tiny-16-more-character-animations): 160×64; Lanea Zimmerman, Clint Bellanger, Charles Gabriel, basxto; CC BY 3.0.

Это лицензированные **замены**, а не установленное происхождение старых `PlayerAll.png` и `allEnemy.png`: точного совпадения для них не найдено. Замена выполнена; карта кадров, изменения и SHA-256 сверены с `character-replacements.json` и результатами Unity.

## Новая графика карточки

`Publishing/Card/icon.png` (512×512) и `cover.png` (800×470) созданы для проекта встроенным `image_gen.imagegen`. Исходные результаты сохранены как `icon-source.png` и `cover-source.png`; запросы — `image-prompts.json`, размеры, преобразования и SHA-256 — `image-report.json`. Описание героя основано на игровом `Assets/Sprits/PlayerAll.png`; создание этих промоиллюстраций не подтверждает происхождение исходного игрового атласа.

## Предыдущие заявления владельца

Первоначально владелец сообщил: «бесплатные ассеты и мои собственные». 13.10.2026 он попросил: «укажи просто что все мои».

Последняя формулировка записана как заявление владельца об авторстве, без независимого подтверждения. Она не меняет найденные сторонние источники. Позднее владелец выбрал замену неподтверждённых материалов; результат приведён ниже. Их старое происхождение не объявляется установленным.

## Итог замены, 17.10.2026

Владелец явно выбрал замену материалов с неподтверждёнными правами. Первоначальные 96 растров дополнены двумя файлами из конечного отчёта Unity; в реестре 98 записей:

- 53 файла имеют точное совпадение с опубликованным оригиналом; ещё 9 — ранее установленные лицензии; 4 медали происходят из подтверждённого CC0-атласа.
- 11 атласов персонажей заменены лицензированными производными Tiny 16 / PlayCraft. Размеры PNG, все 174 sprite rect и Unity GUID сохранены. У скелета удалён одноцветный зелёный фон; кадры и палитры адаптированы. Доказательства и хеши: `character-replacements.json`; воспроизводимый упаковщик: `Tools/ReplaceCharacterSprites.py`.
- 14 PNG интерфейса/эффектов/палатки/объектов арены и рамки созданы процедурной пиксельной геометрией без пикселей старых изображений. `interface-sprite-provenance.json`; `Tools/GenerateInterfaceSprites.py`.
- Неиспользуемые 7 изображений TMP Examples / EmojiOne исключены из корневых Resources; исходники перемещены в Editor. Используемый материал LiberationSans Overlay оставлен по прежнему пути. Проверка зависимостей Unity: `release-asset-dependencies.json`.
- Все 40 неизвестных аудиофайлов заменены собственным синтезом без внешних записей/сэмплов. `AUDIO_PROVENANCE.md`, `audio-provenance.json`, `Tools/GenerateGameAudio.py`. Полное декодирование FFmpeg и импорт всех 40 клипов в Unity прошли.

Карта Tuxemon — Buch, [первоисточник](https://opengameart.org/content/tuxemon-tileset), CC BY-SA 3.0. Полосы эмоций — TheBlueJester, [первоисточник](https://opengameart.org/content/npc-expression-emotion-bubbles-project-emotion), CC BY-SA 3.0. Монеты — Loel, [первоисточник](https://opengameart.org/content/coins-pixel-art-silver-gold-crystal-and-more), из предложенных условий выбрана CC0. Эти четыре PNG совпали побайтово, поэтому сохранены. Дополнительные URL/хеши внесены в `asset-rights-inventory.json`; CC BY-SA 3.0 включена в StreamingAssets.

Подписи авторов, изменения и URL источников/лицензий доступны в игре: **Настройки → Авторы ассетов**. Текст двуязычный, без внешней навигации. Лицензии CC относятся к указанной графике; вся игра не переобъявляется под одной общей лицензией.

Старые неподтверждённые оригиналы сохранены вне Assets в ignored `Builds/AssetReplacements`; они не должны включаться в игру. Это не подтверждает их авторство и не распространяет их как новые материалы.

Проверка конечного списка реально упакованных ассетов, а не только графа GUID, прошла: `Tools/VerifyReleaseAssets.py`, результат **PASS** в `release-assets-validation.json`. В BuildReport — 94 растровых пути, неизвестных среди файлов Assets нет; неиспользуемые демо и резервные старые материалы в список не попали. Правовой реестр фиксирует источники, условия и техническую сверку, а не даёт юридическую гарантию или решение модерации.
