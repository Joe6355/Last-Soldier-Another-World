using PlayerPrefs = RedefineYG.PlayerPrefs;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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

    public bool canShoot = true;         // Разрешение на стрельбу
    private int shootingMode = 1;        // 1 - обычный, 2 - дробовик, 3 - автомат
    private int selectedArrowIndex = 0;  // Текущий тип стрелы

    private Coroutine fadeCoroutine;      // Корутин для плавного исчезновения текстов
    private Coroutine autoShootCoroutine; // Корутин для авто-стрельбы
    private bool isAutoShooting = false;  // Флаг, включена ли авто-стрельба

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
            StopAutoShooting();
            return;
        }

        // Смена режима стрельбы
        if (Input.GetKeyDown(KeyCode.Alpha1)) { shootingMode = 1; UpdateModeText(); }
        else if (Input.GetKeyDown(KeyCode.Alpha2)) { shootingMode = 2; UpdateModeText(); }
        else if (Input.GetKeyDown(KeyCode.Alpha3)) { shootingMode = 3; UpdateModeText(); }

        // Переключение типа стрел
        if (Input.GetKeyDown(KeyCode.Q)) { SwitchArrowType(); }

        // Автоматическая стрельба
        if (shootingMode == 3 && Input.GetMouseButtonDown(0) && !isAutoShooting)
        {
            autoShootCoroutine = StartCoroutine(AutoShoot());
        }
        else if (shootingMode != 3 || Input.GetMouseButtonUp(0))
        {
            StopAutoShooting();
        }
    }

    public void Shoot()
    {
        if (!canShoot || YG.YG2.isPauseGame || Time.timeScale == 0f || arrowCounts[selectedArrowIndex] <= 0) return;

        switch (shootingMode)
        {
            case 1:
                arrowCounts[selectedArrowIndex]--;
                SaveArrowCounts();
                FireArrow(firePoint.position, firePoint.up);
                UpdateArrowCountsUI();
                break;

            case 2:
                if (arrowCounts[selectedArrowIndex] < 3) return;
                StartCoroutine(ShotgunRoutine());
                break;
        }
    }

    private IEnumerator ShotgunRoutine()
    {
        canShoot = false;
        yield return new WaitForSeconds(shotgunDelay);

        arrowCounts[selectedArrowIndex] -= 3;
        SaveArrowCounts();
        FireShotgun();
        UpdateArrowCountsUI();
        canShoot = true;
    }

    private IEnumerator AutoShoot()
    {
        isAutoShooting = true;
        while (canShoot && !YG.YG2.isPauseGame && Time.timeScale > 0f && Input.GetMouseButton(0) && arrowCounts[selectedArrowIndex] > 0)
        {
            FireArrow(firePoint.position, firePoint.up);
            arrowCounts[selectedArrowIndex]--;
            SaveArrowCounts();
            UpdateArrowCountsUI();
            yield return new WaitForSeconds(0.2f);
        }
        isAutoShooting = false;
    }

    private void StopAutoShooting()
    {
        if (isAutoShooting && autoShootCoroutine != null)
        {
            StopCoroutine(autoShootCoroutine);
            isAutoShooting = false;
        }
    }

    private void FireArrow(Vector3 spawnPos, Vector2 direction)
    {
        GameObject arrow = Instantiate(arrowPrefabs[selectedArrowIndex], spawnPos, firePoint.rotation);
        PlaySound(sounds[0], volume: 1, destroyed: true);
        Rigidbody2D rb = arrow.GetComponent<Rigidbody2D>();
        rb.AddForce(direction * fireForce, ForceMode2D.Impulse);
    }

    private void FireShotgun()
    {
        FireArrow(firePoint.position + firePoint.right * -offsetX, Quaternion.Euler(0, 0, +10) * firePoint.up);
        FireArrow(firePoint.position, firePoint.up);
        FireArrow(firePoint.position + firePoint.right * offsetX, Quaternion.Euler(0, 0, -10) * firePoint.up);
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
            arrowCountsText[i].text = arrowCounts[i].ToString();
        }
    }

    private void UpdateArrowTypeIndicator()
    {
        for (int i = 0; i < arrowTypeIndicators.Length; i++)
        {
            arrowTypeIndicators[i].SetActive(i == selectedArrowIndex);
        }
    }

    public void AddArrows(int typeIndex, int amount)
    {
        if (typeIndex >= 0 && typeIndex < arrowCounts.Length)
        {
            arrowCounts[typeIndex] += amount;
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
        if (PlayerPrefs.HasKey("ArrowCounts"))
        {
            string arrowCountsString = PlayerPrefs.GetString("ArrowCounts");
            string[] counts = arrowCountsString.Split(',');

            for (int i = 0; i < counts.Length && i < arrowCounts.Length; i++)
            {
                int.TryParse(counts[i], out arrowCounts[i]);
            }
        }
        else
        {
            ResetArrowCounts();
        }
    }

    public void ResetArrowCounts()
    {
        arrowCounts = new int[] { 100, 50, 30 };
        SaveArrowCounts();
        UpdateArrowCountsUI();
    }
}
