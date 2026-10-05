using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AddHolyArrow : MonoBehaviour
{
    public float lifeTime = 4f;
    private Rigidbody2D rb;
    public int rotSpeed = 1;

    public CrossbowController crossbowController;

    [SerializeField] private int countArrow;
    [SerializeField] private int typeArrow;
    private void Update()
    {
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Destroy(gameObject);
            crossbowController.AddArrows(typeArrow, countArrow);
        }
    }

    private void Start()
    {

        rb = GetComponent<Rigidbody2D>();
        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        rb.AddForce(randomDirection * 2, ForceMode2D.Impulse);

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
        Invoke("Stop", 0.5f);
        Destroy(gameObject, lifeTime);

        SetController(crossbowController);

    }
}
