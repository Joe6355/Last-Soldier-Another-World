# Проверка на телефоне, 30.10.2026

Устройство: модель 23021RAA2Y, Android 13 / API 33, ARM64. Управление — ADB с разрешения владельца, без зеркалирования телефона и управления окнами Windows.

## Настоящая награда и сохранение

QA 1.0.2 / code 4, Unity 2022.3.62f2, Yandex Mobile Ads 8.6.0. Временный runtime-тест из `Tests/RuStoreDeviceRewardCheck.cs` вызывает существующий `GameMonetization.RequestCoinsForVideo(player, 500, ...)`. Он не вызывает `OnRewarded`, не добавляет монеты и не записывает сохранение самостоятельно. Рекламные ID и код адаптера соответствуют production. SHA-256 `Assets/Scripts/RuStore/RuStoreAds.cs`: `4e61097c746a1d9a00eee8fec58cb239bf4269c89ef5943edaa130bf8fc7d411`.

После запроса видео приложение свёрнуто кнопкой Home в 16:12:47.476 и возвращено запуском основной Activity в 16:12:49.633 (UTC+5). Выдержано более 15 секунд, которые раньше могли ошибочно истечь до доставки очереди SDK. SDK доставил настоящие callbacks награды и закрытия; игровой баланс увеличился ровно на 500. После снятия паузы тест завершился `PASS`.

Выдержка из локального журнала `Builds/RuStoreValidation/game-device-reward-sdk86-20261030.log`:

```text
10-30 16:11:58.026 [RuStoreDeviceCheck] BEFORE coins=1000
10-30 16:11:58.312 [Integration] Yandex Mobile Ads 8.6.0 initialized successfully
10-30 16:11:59.564 [RuStoreAds] Rewarded requested
10-30 16:11:59.565 [RuStoreDeviceCheck] REQUEST accepted=True
10-30 16:12:51.017 [RuStoreAds] Rewarded shown
10-30 16:12:51.020 [RuStoreAds] Rewarded earned
10-30 16:12:51.024 Собрано монеток: 1500
10-30 16:12:51.043 [RuStoreAds] Rewarded closed: granted
10-30 16:12:51.046 [RuStoreDeviceCheck] CALLBACK result=Granted granted=500
10-30 16:12:51.072 [RuStoreDeviceCheck] PASS before=1000 after=1500 granted=500 result=Granted
```

Время `shown` здесь — время обработки очереди в Unity после возвращения, а не время появления картинки нативной рекламы.

Затем установлен обновлением постоянный production APK `last-soldier-rustore.apk`, SHA-256 `c11ea2030b7a371dd344ee4222c8bd84906df30564675d66f3d786277ec4217b`. Выполнены `am force-stop`, холодный запуск и нажатие «Играть». Временный runtime-тест удалён из `Assets`, production не содержит QA define. Локальный журнал `production-reward-persistence-20261030.log`:

```text
10-30 16:13:37.046 ApplicationInfo com.joe6355.lastsoldier.rustore version 1.0.2
10-30 16:13:37.460 Product Name: Последний солдат: другой мир
10-30 16:13:40.503 [Integration] Yandex Mobile Ads 8.6.0 initialized successfully
10-30 16:13:42.561 Монеты игрока: 1500
10-30 16:13:42.567 Gameplay Start
```

До этого диагностический прогон с локальной подменой native SDK на 8.5.0 дал 500 → 1000. Он не использован как доказательство для 8.6.0; подмена снята перед приведённым выше повторным прогоном. Production остался на 8.6.0.

## Предел проверки

- Подтверждены выдача и сохранение настоящей награды, возврат из фона и снятие флагов рекламной/игровой паузы. Прослушивание звука после видео этим автоматическим тестом не выполнялось.
- Штатное закрытие кнопкой рекламного SDK, досрочная отмена, видеовоскрешение после смерти, offline и покупки за игровые монеты остаются отдельными Android-сценариями.
- В отдельных прогонах Android Back не закрывал объявление. То же наблюдалось в минимальном native Android-приложении без Unity с SDK 8.6.0; в нём `OnRewarded` пришёл примерно через 11 секунд. Причина поведения закрытия не установлена. Это не доказательство общей ошибки SDK на всех устройствах.
- Код production не принудительно завершает рекламную Activity, не меняет UI SDK и не выдаёт монеты по таймеру.
- На телефоне оставлена обычная production 1.0.2 / code 4 с сохранённым балансом 1500. В RuStore этот APK ещё нужно загрузить и выбрать вместо code 3; публикация не выполнена.
