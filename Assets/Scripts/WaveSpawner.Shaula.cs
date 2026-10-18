using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class WaveSpawner
{
    [Header("Wave 20 — Shaula (normal arena only)")]
    [SerializeField] private ShaulaBoss shaulaPrefab;
    [SerializeField] private GameObject shaulaHealthPanel;
    [SerializeField] private Image shaulaHealthFill, shaulaTailFill;
    [SerializeField] private TMP_Text shaulaTitle, shaulaState;
    private ShaulaBoss shaula;
    private bool shaulaDefeated;
    public ShaulaBoss Shaula => shaula;

    private IEnumerator ActivateShaula()
    {
        shaulaDefeated = false;
        yield return new WaitForSeconds(1);
        if (!isWaveActive || crimsonArena || currentWaveIndex != 19) yield break;
        if (shaulaPrefab == null) { Debug.LogError("Wave 20 Shaula prefab is missing."); CompleteWave(false); yield break; }
        var zone = GetComponent<Collider2D>().bounds;
        Vector3 position = zone.center + new Vector3(0, 3, 0);
        position.z = 0;
        shaula = Instantiate(shaulaPrefab, position, Quaternion.identity);
        shaula.name = "ShaulaWave20";
        shaula.Configure(this, HealthMultiplier, DamageMultiplier, MovementMultiplier, ProjectileSpeedMultiplier,
            shaulaHealthPanel, shaulaHealthFill, shaulaTailFill, shaulaTitle, shaulaState);
        shaula.gameObject.SetActive(true);
        bossActivated = true;
    }
    public void NotifyShaulaDefeated(ShaulaBoss boss)
    {
        if (boss == shaula && !crimsonArena && currentWaveIndex == 19 && isWaveActive && boss.VictoryAwarded)
            shaulaDefeated = true;
    }
    private void HideShaula()
    {
        if (shaula != null)
        {
            shaula.gameObject.SetActive(false); Destroy(shaula.gameObject); shaula = null;
        }
        if (shaulaHealthPanel != null) shaulaHealthPanel.SetActive(false);
        shaulaDefeated = false;
    }
}
