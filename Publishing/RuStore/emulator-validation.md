# Android Emulator — 01.11.2026

После изменения BIOS владельцем аппаратная виртуализация доступна. Эмулятор и Android-образ уже были установлены 26.10; теперь выполнены настройка и успешный запуск. Системные компоненты Windows, политика PowerShell, драйверы и код игры этой задачей не менялись.

## Подтверждено

- AMD Ryzen 5 5600X: `VirtualizationFirmwareEnabled=true`, SLAT и VM Monitor Extensions доступны.
- `emulator -accel-check`: код 0, `AEHD (version 2.2) is installed and usable`; служба `aehd` — `RUNNING`.
- Android Emulator 37.2.12, AVD `LastSoldier_RuStore_API30`, Google APIs Android 11 / API 30 x86_64 rev. 16. `sys.boot_completed=1`.
- Запуск: `-accel on -cores 4 -memory 3072 -gpu auto -feature GLESDynamicVersion`. Android сообщает CPU `0-3`.
- OpenGL ES 3.1: `ro.opengles.version=196609`, `reqGlEsVersion=0x30001`; SurfaceFlinger подтверждает NVIDIA RTX 3070 через Android Emulator OpenGL ES Translator. Без динамического определения GLES Android заявлял ES 2, что вызывало предупреждение Unity. Настроен эмулятор, требования игры не снижались.
- Образ предоставляет ARM64 через `libndk_translation.so`. Установлен тот же release APK 1.0.3 / code 5, `primaryCpuAbi=arm64-v8a`, SHA-256 `6bed2126ed83e99e2a3a34bddc7f84596c054d217e112338d1133828b770757f`.
- Журнал игры: `Init Game Success`, `Монеты игрока: 0`, `Gameplay Start`. Получены изображения главного меню и паузы виртуального Android. Сохранение эмулятора отдельное, данные телефона не переносились.

## Запуск и доказательства

На рабочем столе создан ярлык **Android RuStore**. Он вызывает локальный `Builds/RuStoreValidation/Start-AndroidEmulator.cmd`, задающий SDK/AVD только для запускаемого процесса. ADB: сервер 5038, устройство `emulator-5556`. Повторная команда ярлыка проверена: код 0, второго экземпляра AVD нет. Старые snapshots не загружаются; пользовательские данные AVD сохраняются. Отправка метрик и отчётов отключена параметрами официального эмулятора.

В локальном ignored `Builds/RuStoreValidation` сохранены:

- `emulator-hardware-check-20261101.json` — BIOS, ускорение, Android, ABI и APK.
- `emulator-current.stdout.log`, `emulator-current.stderr.log` — успешный запуск; загрузка Android за 31988 мс.
- `emulator-game-gles3-20261101.log`, `emulator-game-final-20261101.log` — запуск Unity и уровня.
- `emulator-menu-20261101.png`, `emulator-current-20261101.png` — главное меню и пауза эмулятора, не физического телефона и не Editor.
- `emulator-shortcut-test.stdout.log`, `emulator-shortcut-test.stderr.log` — повторный запуск команды ярлыка.

## Ограничения

Проверены загрузка Android, установка APK, графический запуск и вход в уровень. Звук, устойчивый FPS, мультитач, видео и выдача рекламной награды этим прогоном не проверялись. Эмулятор с трансляцией ARM64 не заменяет реальные ARM-устройства. Новая версия RuStore на модерацию или публикацию не отправлялась.

Google рекомендует WHPX и указывает окончание поддержки AEHD 31.12.2026: [официальная инструкция](https://developer.android.com/studio/run/emulator-acceleration). Настройка версий GLES описана в [официальных release notes](https://developer.android.com/studio/releases/emulator).
