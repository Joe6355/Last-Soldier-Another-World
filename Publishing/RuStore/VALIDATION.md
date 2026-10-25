# RuStore: первый релиз без сервера

Дата: 25.10.2026. Это подготовка версии; модерация и публикация ещё не выполнены.

## Итоговый объём

В отдельной ветке `codex/rustore-release` сохранены реклама и локальный прогресс. По решению владельца **удалены общий онлайн-рейтинг, Яндекс-вход, LoginSDK и серверная часть из текущего релиза**. Их локальная заготовка отложена, [размещение не требуется](HOSTING.md).

`main` локально и на origin проверен: `67191d12c3f8da09f238595b66a3b0d37bc94676`. В исходной папке нет отслеживаемых изменений; два ранее созданных untracked-файла аудита Publishing оставлены на месте.

Расширены существующие `YandexBuild`, `GameProgress`, `MainMenuController`, `TopPlayersPanel`, `GameLocalization`, `Ui` и два маршрута Editor-рекламы YG2. Новый код — `Assets/Scripts/RuStore`, `Assets/Editor/YandexBuild.RuStore.cs`, Android Gradle-конфиги и SDK. Игровая механика, графика и экономика не переписывались.

## Проверки

| Проверка | Доказательство и предел |
| --- | --- |
| Android APK | Unity 2022.3.62f2, IL2CPP, AGP 8.7.3 / Gradle 8.9. Итоговый хеш и параметры — `apk-validation.json`. |
| Статический анализ | 19/19: `Tools/Verify-RuStoreApk.py` — release-подпись, CRC, отсутствие debuggable/AD_ID, backup и cleartext выключены, min 26 / target 35, ARM64 ELF 16 КиБ, исправленная Unity; LoginSDK и мосты входа отсутствуют в manifest/DEX. Это не тест установки на телефоне. |
| Unity-сценарии | 20/20: `editor-checks.txt` — локальный старт, скрытые вход/онлайн-рейтинг, RU/EN уведомление, сенсорная раскладка, награда ровно один раз, отмена и ошибка без награды, пауза/звук, запись монет и защита production от demo-рекламы. Использованы vendor Editor stubs. |
| Браузерная регрессия | `node --test Tests/yandex-sdk.test.cjs Tests/webgl-orientation.test.cjs`: 10/10 test entries, включая 9 отдельных сценариев ориентации и существующий набор SDK. WebGL-билд из Android-ветки не выпускался. |
| Интерфейс | Editor-кадры находятся в локальном `Builds/RuStoreValidation/Previews`; это не Android-скриншоты для карточки. |

## Осталось до публикации

- [ ] Три действующих мобильных рекламных блока РСЯ: rewarded/interstitial/banner. Сейчас включены demo ID официального SDK.
- [ ] Проверка реальной рекламы на Android: показ, награда, отмена/ошибка, возвращение звука и управления. Доступного телефона в `adb devices -l` нет.
- [ ] Проверка установки, холодного запуска/offline, мультитача, safe area/вырезов, сворачивания и локальных сохранений на устройстве.
- [ ] Подтверждение сведений оператора и публичной политики, затем `releasePrivacyReviewed=true`. Собственного сервера/профиля в политике больше нет; контакт `dudin1918@mail.ru` указан.
- [ ] Резервная копия ключа, alias и пароля у владельца. Они только в ignored `Builds/Signing`; публичный SHA-256 — в `signing-certificate.txt`.
- [ ] Карточка RuStore: возраст/маркировки по содержимому, реклама и данные SDK, три Android-скриншота, контакт и политика. Черновик — `store-listing.md`.
- [ ] Production APK с `useTestAds=false`, рабочими рекламными ID, той же подписью и актуальным versionCode; проверка и отправка на модерацию. Если QA уже загружался в консоль, повысить versionCode.

Сборщик отклоняет production с demo-рекламой, пустыми рекламными настройками и неподтверждённой политикой. Собственные хостинг, домен и OAuth для первого релиза не требуются.

Официальные источники: [правила RuStore](https://www.rustore.ru/help/developers/publishing-and-verifying-apps/requirement-apps), [публикация карточки](https://www.rustore.ru/help/developers/publishing-and-verifying-apps/app-publication), [патч Unity](https://unity.com/security/sept-2025-01), [Android 16 КиБ](https://developer.android.com/guide/practices/page-sizes).
