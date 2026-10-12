# Медали MMR

Четыре спрайта в `Assets/UI/ForestGarrison/Medals` исправлены: `Emerald.png`, `Sapphire.png`, `Amethyst.png`, `Ruby.png`. Каждый PNG — 48×48, прозрачный фон, пять непрозрачных оттенков. Точная геометрия черепа, ободка, бликов и силуэта перенесена из исходной золотой медали `Assets/Sprits/achievements.png` (x=120, y=465, 48×48 в координатах Unity). Каждый исходный непрозрачный оттенок заменяется одним новым; прозрачные пиксели остаются прозрачными. У оригинала есть тонкие границы в один пиксель, поэтому он не сводится к равномерной сетке 24×24.

Импорт Unity сохранён: Sprite (Single), Point, без mipmaps и сжатия. GUID, имена файлов и ссылки обеих сцен сохранены; старый атлас не изменён. Прежние изображения с изменённым черепом и мелкими деталями заменены.

| Медаль | Минимальный MMR |
| --- | ---: |
| Бронза | 0 |
| Серебро | 100 |
| Золото | 500 |
| Алмаз | 1 000 |
| Изумруд | 2 500 |
| Сапфир | 5 000 |
| Аметист | 10 000 |
| Рубин | 20 000 |

`Stats.MedalForMmr` используется в меню, личной статистике и строках таблицы лидеров. Личный MMR вычисляется существующей формулой `100 × зачищенные волны + все убийства`. Для общего рейтинга используется `LBPlayerData.score`, включая записи без корректного `extraData`. Формула и формат сохранений не изменены; отдельного сохранения медалей нет.

## Палитры и исходник

Пять исходных непрозрачных цветов: `#161513`, `#584A21`, `#6F5B2C`, `#8A7D42`, `#AC9C52` — контур, тёмная гравировка, поверхность, ободок и блик. Порядок замены:

| Спрайт | Контур | Гравировка | Поверхность | Ободок | Блик |
| --- | --- | --- | --- | --- | --- |
| Emerald | #181E18 | #233D2D | #30503C | #567D58 | #75956B |
| Sapphire | #181B20 | #28394C | #344B66 | #58778F | #819EAB |
| Amethyst | #1C181F | #382D42 | #52445F | #776284 | #9B87A5 |
| Ruby | #221819 | #48282D | #633B3F | #8D5556 | #AC7771 |

Для цветового черновика использован встроенный `image_gen` из навыка `imagegen`. Генерация повторно изменила контур; черновые изображения не используются как итоговые PNG. Финальные файлы экспортированы по пиксельным маскам оригинала с фиксированными палитрами. Подготовка исходника — `Builds/RatingValidation/prepare-medal-reference.ps1`; экспорт — `normalize-medal-palette.ps1`. Прежние PNG сохранены в `medals-before-native-correction` внутри исключённого из Git каталога проверок.

Общий prompt цветового черновика:

```text
Use case: precise-object-edit. Edit the supplied ORIGINAL GOLD SKULL MEDAL, which is a 24x24 native pixel sprite nearest-neighbour enlarged to 768x768. This is a STRICT PALETTE SWAP task, not a redraw or illustration. Return one square transparent-background icon showing exactly the same medal. Keep every single 24x24 grid-cell position, contour, proportion, bevel, rim, engraved skull, eye holes, nose and mouth, and the diagonal highlight exactly as in the input. Keep the skull SMALL, low contrast and engraved, not a large cartoon skull. Keep the medal frontal and flat. The native geometry is an octagon with short stepped corners. Each colour area is flat; the image contains only 5 or 6 opaque colours plus transparency. Pixels within each native cell must be one perfectly uniform solid colour. Keep transparent cells transparent and opaque cells fully opaque. Absolutely no added pixels or outlines, no antialiasing, no smooth gradient, texture, sheen, shiny stripes, gemstones, ornament, realistic rendering, lighting bloom, shadows outside the badge or text. Preserve the subdued, dark original retro pixel-art treatment, including the charcoal outlines and black holes. ONLY replace the GOLD COLOURS using this palette: {palette}. Keep the remaining structural dark pixels unchanged. Produce the same exact crisp 24x24 pixel-art geometry as the reference, displayed large with nearest-neighbour pixel blocks. This is a recoloured replacement for an existing Unity medal; it must retain the original skull anatomy and silhouette.
```

Палитры запроса — пять цветов из таблицы выше: emerald/green, sapphire/blue, amethyst/purple, ruby/red. Указание 24×24 было предварительной оценкой; попиксельная проверка выявила однопиксельные границы в оригинале. Поэтому окончательный экспорт сохраняет точную сетку 48×48. Запрос генератору не означает, что его результат точно сохранил геометрию.

## Проверки

12.10.2026 прошли компиляция и `HigherMedalChecks.Run` в отдельной копии Unity 2022.3.47f1: обе сцены, импорт восьми спрайтов, каждый порог MMR и соседние значения, профиль/личный результат, серверный MMR при отсутствующей или противоречивой статистике, замена заглушки настоящим результатом. Для каждого нового PNG проверено попиксельное совпадение альфа-силуэта с исходной золотой медалью, однозначная замена пяти цветов и отсутствие полупрозрачных пикселей.

Настоящие карточки профиля и рейтинга отрисованы в обеих сценах при 1280×720 и 854×393: восемь снимков `Builds/RatingValidation/revised-medals-{menu|arena}-{разрешение}[-profile].png`. Личная медаль — аметист при 10 127 MMR; строки содержат все четыре новых уровня. На маленьком экране медаль личного результата занимает около 38 px, строки — около 33 px. Проверяемые локальные ключи и SDK-сохранение до/после проверки совпали; тест работает в Edit Mode и не отправляет запросы в Яндекс.

Отчёт — `Builds/RatingValidation/higher-medal-checks.txt`, журнал — `revised-medal-checks.log`; совпадение 72 файлов основной и проверочной копий — `higher-medal-source-match.json`. Превью — `native-medals-preview.png`.

ПК-сборка после замены PNG прошла: `RATING_BUILD_PASS: Succeeded; errors=0; bytes=116600851`. Все четыре спрайта включены в список используемых ресурсов сборки. Журнал — `Builds/RatingValidation/revised-medal-pc-build.log`; Unity завершилась кодом 0. `git diff --check` прошёл.

Для проверки в игре открыть рейтинг после достижения соответствующего MMR. Для всех порогов без изменения игрового прогресса повторить `HigherMedalChecks.Run` в проверочной копии. Не менять счётчики в рабочей Play Mode-сессии: игра может сохранить тестовый прогресс автоматически. Установленный APK 1.0.7 пока содержит прежние медали.
