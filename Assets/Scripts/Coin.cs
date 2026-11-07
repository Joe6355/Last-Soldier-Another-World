using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Coin: Sounds
{
    [SerializeField] private int coinValue = 1; //сколько дает монетка при сборе
    [SerializeField] private int lifeTime = 15;//время жизни монетки
    private Rigidbody2D rb;
    private PlayerController lootPlayer;
    private static PlayerController sceneLootPlayer;
    private bool collected;
    private bool beingAttracted;
    private bool trainingCoin;
    private System.Action trainingCollected;

    // Configure immediately after spawning, before Start schedules the normal expiry.
    public void ConfigureTrainingCoin(int value, System.Action callback)
    {
        coinValue = Mathf.Max(0, value);
        trainingCoin = true;
        trainingCollected = callback;
        rb = GetComponent<Rigidbody2D>();
        if (rb != null) Stop();
    }
    public int rotSpeed = 1;   
    public void ScaleReward(int multiplier, int divisor = 1)
    {
        divisor = Mathf.Max(1, divisor);
        long scaled = (long)coinValue * Mathf.Max(1, multiplier);
        // Random rounding preserves the expected reward even for a one-coin drop.
        long rounded = scaled / divisor;
        if (scaled % divisor > 0 && Random.Range(0, divisor) < scaled % divisor) rounded++;
        coinValue = (int)System.Math.Min(int.MaxValue, rounded);
    }

    private void Start()
    {
          
        rb = GetComponent<Rigidbody2D>();
        if (sceneLootPlayer == null) sceneLootPlayer = FindObjectOfType<PlayerController>();
        lootPlayer = sceneLootPlayer;
        if (trainingCoin)
        {
            Stop();
            return;
        }
        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        rb.AddForce(randomDirection * 2, ForceMode2D.Impulse);
        Invoke(nameof(Stop), .5f);
        Destroy(gameObject, lifeTime);

    }


    void Stop()
    {
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }
    private void FixedUpdate()
    {
        transform.Rotate(new Vector3(0, rotSpeed, 0));
        if (lootPlayer != null) beingAttracted = lootPlayer.TryAttractLoot(rb, beingAttracted);

    }
    private void OnTriggerEnter2D(Collider2D coll)
    {
        if (!collected && coll.CompareTag("Player"))
        {
            PlayerController player = coll.GetComponentInParent<PlayerController>();

            if (player != null)
            {
                collected = true;
                PlaySound(sounds[0], volume: 1, destroyed: true);
                player.AddCoin(coinValue);
                if (trainingCoin) trainingCollected?.Invoke();
            }

            Destroy(gameObject);
        }
    }

}
