using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Coin: Sounds
{
    [SerializeField] private int coinValue = 1; //сколько дает монетка при сборе
    [SerializeField] private int lifeTime = 15;//время жизни монетки
    private Rigidbody2D rb;
    public int rotSpeed = 1;   
    private void Start()
    {
          
        rb = GetComponent<Rigidbody2D>();
        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        rb.AddForce(randomDirection * 2, ForceMode2D.Impulse);

    }


    void Stop()
    {
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }
    private void FixedUpdate()
    {
        transform.Rotate(new Vector3(0, rotSpeed, 0));
        Invoke("Stop", 0.5f);
        Destroy(gameObject, lifeTime);  

    }
    private void OnTriggerEnter2D(Collider2D coll)
    {
        if (coll.CompareTag("Player"))
        {
            PlayerController player = coll.GetComponent<PlayerController>();

            if (player != null)
            {
                PlaySound(sounds[0], volume: 1, destroyed: true);
                player.AddCoin(coinValue);           
            }

            Destroy(gameObject);
        }
    }

}
