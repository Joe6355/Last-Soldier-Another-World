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
    private bool IsShaulaWave => !crimsonArena && (currentWaveIndex == 19 || IsFinalBossWave);

    private IEnumerator ActivateShaula()
    {
        shaulaDefeated = false;
        yield return new WaitForSeconds(1);
        if (!isWaveActive || !IsShaulaWave) yield break;
        if (!SpawnShaula()) { CompleteWave(false); yield break; }
        bossActivated = true;
    }
    private bool SpawnShaula()
    {
        if (shaulaPrefab == null) { Debug.LogError("Shaula prefab is missing."); return false; }
        shaulaDefeated = false;
        var zone = GetComponent<Collider2D>().bounds;
        Vector3 position = zone.center + new Vector3(0, 3, 0);
        position.z = 0;
        shaula = Instantiate(shaulaPrefab, position, Quaternion.identity);
        shaula.name = IsFinalBossWave ? "ShaulaWave25" : "ShaulaWave20";
        shaula.Configure(this, HealthMultiplier, DamageMultiplier, MovementMultiplier, ProjectileSpeedMultiplier,
            shaulaHealthPanel, shaulaHealthFill, shaulaTailFill, shaulaTitle, shaulaState);
        shaula.gameObject.SetActive(true);
        return true;
    }
    public void NotifyShaulaDefeated(ShaulaBoss boss)
    {
        if (boss == shaula && IsShaulaWave && isWaveActive && boss.VictoryAwarded)
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
