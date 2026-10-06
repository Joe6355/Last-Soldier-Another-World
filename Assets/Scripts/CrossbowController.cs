using PlayerPrefs = RedefineYG.PlayerPrefs;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CrossbowController : Sounds
{
    [Header("Префабы и настройки")]
    [SerializeField] public GameObject[] arrowPrefabs;  // Массив префабов стрел
    [SerializeField] private Transform firePoint;        // Точка выстрела
    [SerializeField] private int[] arrowCounts;          // Количество стрел каждого типа
    [SerializeField] private float fireForce = 35f;      // Сила стрельбы

    [Header("Shotgun Settings")]
    [SerializeField] private float offsetX = 0.3f;       // Насколько смещаемся влево/вправо (для дробовика)
    [SerializeField] private float shotgunDelay = 0.3f;  // Задержка перед выстрелом дробовика

    [Header("UI Elements")]
    [SerializeField] private Text modeText;              // Текст: режим стрельбы (Обычный, Дробовик, Автомат)
    [SerializeField] private Text[] arrowCountsText;     // Текстовое отображение кол-ва стрел
    [SerializeField] private GameObject[] arrowTypeIndicators; // UI-индикаторы типов стрел
    [SerializeField] private TextMeshProUGUI selectedArrowText;
    [SerializeField] private Image[] modeIndicators = System.Array.Empty<Image>();
    public int SelectedArrowIndex => selectedArrowIndex;
    public int ShootingMode => shootingMode;
    public int GetArrowCount(int typeIndex) => typeIndex >= 0 && typeIndex < arrowCounts.Length ? arrowCounts[typeIndex] : 0;

    public bool canShoot = true;         // Разрешение на стрельбу
    private int shootingMode = 1;        // 1 - обычный, 2 - дробовик, 3 - автомат
    private int selectedArrowIndex = 0;  // Текущий тип стрелы
    private bool shotgunUnlocked, automaticUnlocked;
    private int[] damageLevels = new int[4], speedLevels = new int[4], reloadLevels = new int[4];
    private float nextShotTime;
    public static string ArrowTitle(int index) => index == 0 ? "Обычные стрелы" : index == 1 ? "Ядовитые стрелы" : index == 2 ? "Святые стрелы" : "Пробивные стрелы";
    public bool IsModeUnlocked(int mode) => mode == 1 || mode == 2 && shotgunUnlocked || mode == 3 && automaticUnlocked;
    public float ArrowSpeed(int type, int extraLevels = 0) => fireForce * (1f + .1f * (speedLevels[type] + extraLevels));
    public float ShotInterval(int type, int mode = 1, int extraLevels = 0) => Mathf.Max(.08f, (mode == 3 ? .2f : .35f) / (1f + .12f * (reloadLevels[type] + extraLevels)));
    public int ArrowDamage(int type) => arrowPrefabs[type].GetComponent<ArrowDef>().damage + damageLevels[type];

    private Coroutine fadeCoroutine;      // Корутин для плавного исчезновения текстов

    private void Start()
    {
        LoadArrowCounts();
        UpdateArrowCountsUI();
        UpdateArrowTypeIndicator();

        UpdateModeText(); // Показываем «Обычный режим» при старте
    }

    private void Update()
    {
        UpdateArrowCountsUI();
        if (!GameProgress.IsReady || YG.YG2.isPauseGame || Time.timeScale == 0f)
        {
            return;
        }

        // Смена режима стрельбы
        if (Input.GetKeyDown(KeyCode.Alpha1)) TrySelectMode(1);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) TrySelectMode(2);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) TrySelectMode(3);

        // Переключение типа стрел
        if (Input.GetKeyDown(KeyCode.Q)) { SwitchArrowType(); }

        // Автоматическая стрельба
        if (shootingMode == 3 && Input.GetMouseButton(0)) Shoot();
    }

    public void Shoot()
    {
        if (!GameProgress.IsReady || !canShoot || !IsModeUnlocked(shootingMode) || YG.YG2.isPauseGame || Time.timeScale == 0f || Time.time < nextShotTime || arrowCounts[selectedArrowIndex] <= 0) return;

        switch (shootingMode)
        {
            case 1:
            case 3:
                nextShotTime = Time.time + ShotInterval(selectedArrowIndex, shootingMode);
                arrowCounts[selectedArrowIndex]--;
                SaveArrowCounts();
                FireArrow(firePoint.position, firePoint.up, selectedArrowIndex);
                UpdateArrowCountsUI();
                break;

            case 2:
                if (arrowCounts[selectedArrowIndex] < 3) return;
                nextShotTime = Time.time + shotgunDelay + ShotInterval(selectedArrowIndex);
                StartCoroutine(ShotgunRoutine(selectedArrowIndex));
                break;
        }
    }

    private IEnumerator ShotgunRoutine(int type)
    {
        canShoot = false;
        arrowCounts[type] -= 3;
        SaveArrowCounts();
        UpdateArrowCountsUI();
        yield return new WaitForSeconds(shotgunDelay);
        FireShotgun(type);
        canShoot = true;
    }

    private void FireArrow(Vector3 spawnPos, Vector2 direction, int type)
    {
        GameObject arrow = Instantiate(arrowPrefabs[type], spawnPos, firePoint.rotation);
        arrow.GetComponent<ArrowDef>().damage = ArrowDamage(type);
        PlaySound(sounds.Length > 0 ? sounds[0] : null, volume: 1, destroyed: true);
        Rigidbody2D rb = arrow.GetComponent<Rigidbody2D>();
        rb.AddForce(direction * ArrowSpeed(type), ForceMode2D.Impulse);
    }

    private void FireShotgun(int type)
    {
        FireArrow(firePoint.position + firePoint.right * -offsetX, Quaternion.Euler(0, 0, +10) * firePoint.up, type);
        FireArrow(firePoint.position, firePoint.up, type);
        FireArrow(firePoint.position + firePoint.right * offsetX, Quaternion.Euler(0, 0, -10) * firePoint.up, type);
    }

    public bool TrySelectMode(int mode)
    {
        if (mode < 1 || mode > 3 || !IsModeUnlocked(mode)) return false;
        shootingMode = mode;
        UpdateModeText();
        return true;
    }

    public void RestoreWeaponUpgrades(Beka.UpgradeItem[] items)
    {
        shotgunUnlocked = automaticUnlocked = false;
        System.Array.Clear(damageLevels, 0, damageLevels.Length);
        System.Array.Clear(speedLevels, 0, speedLevels.Length);
        System.Array.Clear(reloadLevels, 0, reloadLevels.Length);
        foreach (var item in items)
        {
            if (item.itemType == Beka.UpgradeItemType.Shotgun) shotgunUnlocked |= item.purchaseCount > 0;
            else if (item.itemType == Beka.UpgradeItemType.Automatic) automaticUnlocked |= item.purchaseCount > 0;
            else if (item.arrowType >= 0 && item.arrowType < 4)
            {
                int count = Mathf.Clamp(item.purchaseCount, 0, item.maxPurchases > 0 ? item.maxPurchases : 10);
                if (item.itemType == Beka.UpgradeItemType.TypeDamage) damageLevels[item.arrowType] = count;
                if (item.itemType == Beka.UpgradeItemType.ArrowSpeed) speedLevels[item.arrowType] = count;
                if (item.itemType == Beka.UpgradeItemType.ReloadSpeed) reloadLevels[item.arrowType] = count;
            }
        }
        if (!IsModeUnlocked(shootingMode)) shootingMode = 1;
        UpdateModeText();
    }

    public void SwitchArrowType()
    {
        selectedArrowIndex--;
        if (selectedArrowIndex < 0)
            selectedArrowIndex = arrowPrefabs.Length - 1;

        UpdateArrowTypeIndicator();
    }

    private void UpdateModeText()
    {
        for (int i = 0; i < modeIndicators.Length; i++)
            if (modeIndicators[i] != null)
            {
                bool selected = i + 1 == shootingMode, unlocked = IsModeUnlocked(i + 1);
                modeIndicators[i].color = selected ? new Color32(227, 186, 101, 255) : new Color32(35, 68, 59, 255);
                var label = modeIndicators[i].GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.color = selected ? new Color32(20, 43, 38, 255) : new Color32(244, 240, 223, (byte)(unlocked ? 255 : 65));
                var lockMark = modeIndicators[i].transform.Find("ModeLock");
                if (lockMark != null) lockMark.gameObject.SetActive(!unlocked);
            }
        modeText.text = shootingMode switch
        {
            1 => "Обычный режим",
            2 => "Дробовик",
            3 => "Автоматический режим",
            _ => "Неизвестный режим"
        };

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeModeText());
    }

    private IEnumerator FadeModeText()
    {
        float duration = 2f;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsedTime / duration);
            modeText.color = new Color(modeText.color.r, modeText.color.g, modeText.color.b, alpha);
            yield return null;
        }

        modeText.color = new Color(modeText.color.r, modeText.color.g, modeText.color.b, 0f);
    }

    private void UpdateArrowCountsUI()
    {
        for (int i = 0; i < arrowCounts.Length; i++)
        {
            int count = arrowCounts[i];
            arrowCountsText[i].text = count >= 1_000_000
                ? $"{count / 1_000_000:N0}\n{count / 1_000 % 1_000:000} {count % 1_000:000}"
                : count.ToString("N0");
        }
    }

    private void UpdateArrowTypeIndicator()
    {
        if (selectedArrowText != null) selectedArrowText.text = ArrowTitle(selectedArrowIndex);
        for (int i = 0; i < arrowTypeIndicators.Length; i++)
        {
            arrowTypeIndicators[i].SetActive(i == selectedArrowIndex);
        }
    }

    public void AddArrows(int typeIndex, int amount)
    {
        if (typeIndex >= 0 && typeIndex < arrowCounts.Length)
        {
            arrowCounts[typeIndex] = (int)System.Math.Min(int.MaxValue, System.Math.Max(0L, (long)arrowCounts[typeIndex] + amount));
            SaveArrowCounts();
            UpdateArrowCountsUI();
        }
    }

    public void SetShootingState(bool state)
    {
        canShoot = state;
    }

    private void OnApplicationQuit()
    {
        SaveArrowCounts();
    }

    public void SaveArrowCounts()
    {
        string arrowCountsString = string.Join(",", arrowCounts);
        PlayerPrefs.SetString("ArrowCounts", arrowCountsString);
        GameProgress.RequestSave();
    }

    public void LoadArrowCounts()
    {
        System.Array.Resize(ref arrowCounts, arrowPrefabs.Length);
        if (PlayerPrefs.HasKey("ArrowCounts"))
        {
            string arrowCountsString = PlayerPrefs.GetString("ArrowCounts");
            string[] counts = arrowCountsString.Split(',');

            for (int i = 0; i < counts.Length && i < arrowCounts.Length; i++)
            {
                if (int.TryParse(counts[i], out int count)) arrowCounts[i] = Mathf.Max(0, count);
            }
        }
        else
        {
            ResetArrowCounts();
        }
    }

    public void ResetArrowCounts()
    {
        arrowCounts = new int[arrowPrefabs.Length];
        if (arrowCounts.Length > 0) arrowCounts[0] = 100;
        if (arrowCounts.Length > 1) arrowCounts[1] = 50;
        if (arrowCounts.Length > 2) arrowCounts[2] = 30;
        SaveArrowCounts();
        UpdateArrowCountsUI();
    }
}
