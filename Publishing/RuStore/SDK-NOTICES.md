# Подключённые SDK RuStore

- **Yandex Mobile Ads Unity 8.6.0 Lite**: [официальный релиз](https://github.com/yandexmobile/yandex-ads-unity-plugin/releases/tag/8.6.0), файл `yandex-mobileads-lite-8.6.0.unitypackage`, SHA-256 `0d0a2546dd752d6b02864df5833767703ae88d5b292da0e0b8eec815d7f066c3`. Импортированы `Assets/YandexMobileAds`, `Assets/AdRevenue`, vendor-файлы `Assets/Plugins`. Исходные copyright/license headers сохранены. Условия, указанные в исходниках SDK: [РСЯ](https://legal.yandex.com/partner_ch/).
- **Android Mobile Ads 8.6.0**, **AppMetrica Analytics 8.5.1**: версии подтверждены разрешённым `releaseRuntimeClasspath` Gradle тестовой сборки. В vendor XML запрошена Analytics 8.0.0, транзитивное ограничение Mobile Ads выбирает 8.5.1. Полный локальный отчёт — `Builds/RuStoreValidation/dependencies.txt`.
- **Яндекс LoginSDK и собственные мосты входа исключены** из первого релиза. Проверка отсутствия выполняется по manifest/DEX итогового APK.
- **Plugin Your Games 2.0092** уже был в проекте. Нативная версия расширяет его существующие интерфейсы; особенности локальных изменений описаны в `Assets/PluginYourGames/LOCAL_CHANGES.md`.

Остальные версии AndroidX/Kotlin и служебных зависимостей приведены в отчёте Gradle. Сведения о данных SDK нужно сверять с фактической release-сборкой и декларацией магазина. Этот файл не заменяет условия поставщиков, настройку кабинетов и подтверждение прав на игровые материалы. Для игровых ассетов сохранён исходный реестр `Publishing/ASSET_RIGHTS.md` и заявление владельца от 21.10.2026.
