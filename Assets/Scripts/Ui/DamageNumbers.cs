using System;
using System.Globalization;
using TMPro;
using UnityEngine;

// World-space numbers share one bounded pool and survive the enemy's death.
public sealed class DamageNumbers : MonoBehaviour
{
    private const string PreferenceKey = "ShowDamageNumbers";
    private static DamageNumbers instance;
    public static bool IsEnabled { get; private set; } = true;

    [SerializeField] private TextMeshPro numberPrefab;
    [SerializeField, Min(1)] private int maximumNumbers = 64;
    [SerializeField, Min(.1f)] private float lifetime = .6f;
    [SerializeField] private float riseDistance = .65f;

    private sealed class Number
    {
        public TextMeshPro label;
        public Vector3 origin;
        public float age, drift;
        public bool active;
    }
    private Number[] numbers;
    private int created;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        IsEnabled = PlayerPrefs.GetInt(PreferenceKey, 1) != 0;
    }

    private void Awake()
    {
        instance = this;
        numbers = new Number[Mathf.Clamp(maximumNumbers, 1, 128)];
        if (numberPrefab != null)
            for (int i = 0; i < Mathf.Min(16, numbers.Length); i++) CreateNumber();
    }

    public static void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        PlayerPrefs.SetInt(PreferenceKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        if (!enabled && instance != null) instance.Clear();
    }

    public static void Show(Transform target, double previousHealth, double currentHealth)
    {
        double damage = Math.Max(0, previousHealth - Math.Max(0, currentHealth));
        if (!IsEnabled || instance == null || !instance.isActiveAndEnabled || target == null || damage <= 0
            || double.IsNaN(damage) || double.IsInfinity(damage)) return;
        instance.Display(target, damage);
    }

    private Number CreateNumber()
    {
        var label = Instantiate(numberPrefab, transform);
        label.gameObject.SetActive(false);
        var number = new Number { label = label };
        numbers[created++] = number;
        return number;
    }

    private void Display(Transform target, double damage)
    {
        if (numberPrefab == null) return;
        Number number = null, oldest = null;
        for (int i = 0; i < created; i++)
        {
            if (!numbers[i].active) { number = numbers[i]; break; }
            if (oldest == null || numbers[i].age > oldest.age) oldest = numbers[i];
        }
        if (number == null) number = created < numbers.Length ? CreateNumber() : oldest;

        var sprite = target.GetComponent<SpriteRenderer>();
        Vector3 point = sprite != null
            ? new Vector3(sprite.bounds.center.x, sprite.bounds.max.y + .05f, target.position.z)
            : target.position + Vector3.up * .5f;
        number.origin = point + Vector3.right * UnityEngine.Random.Range(-.12f, .12f);
        number.drift = UnityEngine.Random.Range(-.18f, .18f);
        number.age = 0;
        number.active = true;
        number.label.text = damage.ToString("0.##", CultureInfo.CurrentCulture);
        number.label.alpha = 1;
        number.label.transform.position = number.origin;
        number.label.transform.localScale = Vector3.one * 1.15f;
        number.label.gameObject.SetActive(true);
    }

    private void Update()
    {
        if (Time.timeScale <= 0 || YG.YG2.isPauseGame) return;
        for (int i = 0; i < created; i++)
        {
            var number = numbers[i];
            if (!number.active) continue;
            number.age += Time.deltaTime;
            float progress = Mathf.Clamp01(number.age / lifetime);
            if (progress >= 1) { Hide(number); continue; }
            number.label.transform.position = number.origin + new Vector3(number.drift * progress, riseDistance * progress, 0);
            number.label.transform.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, Mathf.Min(1, progress * 4));
            number.label.alpha = 1f - Mathf.InverseLerp(.3f, 1f, progress);
        }
    }

    private static void Hide(Number number)
    {
        number.active = false;
        if (number.label != null) number.label.gameObject.SetActive(false);
    }

    private void Clear()
    {
        for (int i = 0; i < created; i++) Hide(numbers[i]);
    }

    private void OnDisable() { Clear(); }
    private void OnDestroy() { if (instance == this) instance = null; }
}
