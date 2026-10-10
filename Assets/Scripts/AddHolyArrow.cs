using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AddHolyArrow : MonoBehaviour
{
    public float lifeTime = 4f;
    private Rigidbody2D rb;
    private PlayerController lootPlayer;
    private bool collected;
    private bool beingAttracted;
    public int rotSpeed = 1;

    public CrossbowController crossbowController;

    [SerializeField] private int countArrow;
    [SerializeField] private int typeArrow;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collected && crossbowController != null && collision.CompareTag("Player"))
        {
            collected = true;
            crossbowController.AddArrows(typeArrow, countArrow);
            Destroy(gameObject);
        }
    }

    private void Start()
    {

        rb = GetComponent<Rigidbody2D>();
        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        rb.AddForce(randomDirection * 2, ForceMode2D.Impulse);
        lootPlayer = FindObjectOfType<PlayerController>();
        Invoke(nameof(Stop), .5f);
        Destroy(gameObject, lifeTime);

        crossbowController = FindObjectOfType<CrossbowController>();

    }

    public void SetController(CrossbowController controller)
    {
        crossbowController = controller;
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

        SetController(crossbowController);

    }
}
