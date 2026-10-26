# Первый релиз RuStore: реклама и локальная игра

Ветка `codex/rustore-release`, отдельный worktree. Оригинальная игра Яндекс Игр остаётся в `main`. По решению владельца первый релиз **без Яндекс-входа и общего онлайн-рейтинга**: сервер, домен и облачный аккаунт не требуются. Локальная статистика, достижения, прогресс и рекламные бонусы сохранены; переноса сохранений между устройствами нет.

## Настройки перед production

1. Выполнено 26.10.2026: в [РСЯ](https://partner.yandex.ru/) создано ещё не опубликованное Android-приложение `20283319` и блоки rewarded `R-M-20283319-1`, interstitial `R-M-20283319-2`, banner `R-M-20283319-3`. ID внесены в `Assets/Resources/RuStoreSettings.asset`; `useTestAds=false`. Пароли и Client Secret не нужны. Пакет игры — `com.joe6355.lastsoldier.rustore`; после публикации его нужно связать с приложением РСЯ через данные магазина.
2. Проверить рекламу на устройстве. До публикации РСЯ возвращает тестовые объявления на созданных ID; доход не начисляется. После появления игры в RuStore добавить ссылку магазина в РСЯ, подтвердить доступ и пройти модерацию до статуса «Активно». Имитатора видео Android Test в APK нет. [Добавление приложения](https://ads.yandex.com/helpcenter/ru/monetization/app/add), [статусы РСЯ](https://ads.yandex.com/helpcenter/ru/monetization/app/status).
3. Выполнено 26.10.2026: владелец указал оператора данных **Дудин Сергей** и подтвердил продолжение в кабинете издателя «Отченаш Сергей». Контакт поддержки: `dudin1918@mail.ru`. [Политика](privacy-policy.md) и локальное RU/EN уведомление обновлены; `releasePrivacyReviewed=true`. Публичная ссылка проверена (HTTP 200, имя оператора совпадает) и внесена в поле сайта карточки.
4. Сохранить постоянный ключ подписи, alias и пароль в собственной защищённой резервной копии. Они находятся только в ignored `Builds/Signing`. Публичный отпечаток — [signing-certificate.txt](signing-certificate.txt). Утраченный ключ мешает обычным обновлениям.
5. В RuStore создано приложение `2063776490` и сохранён [черновик версии `2064910836`](https://console.rustore.ru/apps/2063776490/versions/2064910836/edit). APK, иконка, описание, контакт, политика, «Экшен», 12+ и «жестокость» сохранены. Завершить [карточку](store-listing.md): сверить данные SDK, добавить минимум три реальных Android-скриншота, пройти тестирование и модерацию RuStore. Публикация в черновике выбрана вручную. QA APK не отправлять как публичный релиз.

## Сборка

```powershell
./Tools/Build-RuStore.ps1 -Mode QA
# После завершения настроек и проверки политики:
./Tools/Build-RuStore.ps1 -Mode Release
```

Скрипт экспортирует Gradle-проект через Unity **2022.3.62f2**, JDK 11 / NDK 23.1, затем собирает APK через **Gradle 8.9 / AGP 8.7.3 / JDK 17**. Burst — **1.8.26**, IL2CPP, ARMv7 + ARM64, OpenGL ES 3, min Android API 26 / target 35. Это настройки проекта; target API не назван здесь отдельным обязательным требованием RuStore.

QA: `Builds/RuStore/last-soldier-rustore-qa.apk`. Production: `Builds/RuStore/last-soldier-rustore.apk`. Пакет `com.joe6355.lastsoldier.rustore`, начальная версия `1.0.0` / code `1`. После первой загрузки увеличивать versionCode, сохранять пакет и подпись.

В Unity меню **Tools → Last Soldier → RuStore** сначала выполнить **Prepare QA** или **Prepare release**, дождаться компиляции, затем **Export ... Gradle project**. Для упаковки уже экспортированного проекта запустить скрипт с соответствующим `-Mode` и `-SkipExport`. При изменении исходников требуется новый экспорт. Production отклоняется, если включены demo ID, пусты рекламные настройки или политика не подтверждена.

`Builds/Toolchain` и ключ не входят в Git. Для нового checkout установить туда Unity 2022.3.62f2 с Android Build Support, SDK API 35, Gradle 8.9, JDK 17; NDK 23.1.7779620 и JDK 11 для Unity берутся из `Editor/Data/PlaybackEngines/AndroidPlayer`. Иные пути задаются `LAST_SOLDIER_ANDROID_SDK`, `LAST_SOLDIER_ANDROID_NDK`, `LAST_SOLDIER_UNITY_JDK`. В текущей установке NDK/JDK доступны через junction к read-only компонентам прежней Unity; запись в них не производится. Две Unity на этой папке одновременно не запускать.

Для подписи используется ignored `Builds/Signing/signing.local.json` с полями `keystore`, `alias`, `passwordFile` — абсолютные локальные пути и alias `lastsoldier-rustore`. Иное расположение задаётся `LAST_SOLDIER_SIGNING_CONFIG`. Пароль не хранится в Git и очищается из PlayerSettings после сборки.

## Проверки

- `node --test Tests/yandex-sdk.test.cjs Tests/webgl-orientation.test.cjs` — регрессия существующего браузерного моста.
- Для Unity-проверки скопировать `Tests/RuStoreChecks.cs` и `Tests/LocalizationChecks.cs` в временный `Assets/Editor/RuStoreValidation`, запустить Unity с `-buildTarget Android -executeMethod RuStoreChecks.Run -batchmode` без `-quit`/`-nographics`. Проверка сама завершает Editor; результат — `Builds/RuStoreValidation/editor-checks.txt`. Затем убрать временную папку и откатить изменения ресурсов, сделанные Editor-тестом; их не включать в APK/коммит. Проверки используют отдельный тестовый company/product и vendor Editor stubs.
- `Tools/Verify-RuStoreApk.py` с аргументами APK, `--build-tools`, `--java-home`, `--out` проверяет подпись, manifest, DEX, CRC и ARM64 ELF. LoginSDK и собственные мосты входа должны отсутствовать.
- Телефон: проверить холодный запуск/offline, мультитач и вырезы экрана, смерть/награду/отмену, покупки за игровые монеты, сворачивание и повторный запуск/сохранения. Editor-тесты этого не заменяют.

Результаты и ограничения — в [VALIDATION](VALIDATION.md). Серверная заготовка отложена и сохранена только локально: [статус](HOSTING.md).

## Проверка без телефона

Локально подготовлен официальный Android Emulator 37.2.12, Google APIs Android 11 x86_64 rev. 16 и AVD `LastSoldier_RuStore_API30` в ignored `Builds/AndroidUserHome`. Лицензия Android SDK принята после явного подтверждения владельца. Без аппаратной виртуализации программный запуск не дал работающего Android; результат тестирования APK и скриншоты не получены.

Для рекомендуемого запуска нужны включённая в BIOS SVM/VT-x и Windows Hypervisor Platform (WHPX), затем перезагрузка. [Инструкция Android](https://developer.android.com/studio/run/emulator-acceleration). Настройки системы этой задачей не менялись. Локальная инструкция для текущего компьютера — `Builds/RuStoreValidation/EMULATOR_SETUP.md`. После подготовки среды повторить `emulator -accel-check`, запустить AVD, установить тот же APK и пройти список проверок выше. Эмулятор не подтверждает поведение на всех физических устройствах.
