using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeadZone : MonoBehaviour
{
 
   
    public float damage = 60;
    [SerializeField] private WaveSpawner arenaOwner;
    [SerializeField] private Collider2D safeArea;


    private PlayerController playerController;
   

    private void Start()
    { 
        playerController = FindObjectOfType<PlayerController>();
    }

    private void LateUpdate()
    {
        if (arenaOwner == null || !arenaOwner.IsRunning || safeArea == null || playerController == null
            || playerController.hp <= 0 || Time.timeScale <= 0 || YG.YG2.isPauseGame) return;
        if (!safeArea.OverlapPoint(playerController.transform.position)) DamageAndConstrain();
    }

    private void DamageAndConstrain()
    {
        if (playerController == null || (safeArea != null && safeArea.OverlapPoint(playerController.transform.position))) return;
        playerController.TakeDamage(damage);
        if (safeArea == null || arenaOwner == null || !arenaOwner.IsRunning) return;
        Vector2 point = safeArea.ClosestPoint(playerController.transform.position);
        point += ((Vector2)safeArea.bounds.center - point).normalized * .4f;
        playerController.transform.position = new Vector3(point.x, point.y, playerController.transform.position.z);
        var body = playerController.GetComponent<Rigidbody2D>();
        if (body != null) { body.position = point; body.velocity = Vector2.zero; }
    }



    private void OnTriggerStay2D(Collider2D coll)
    {
        if (coll.gameObject.CompareTag("Player"))
        {
            if (safeArea == null) playerController.TakeDamage(damage);
            else if (arenaOwner != null && arenaOwner.IsRunning) DamageAndConstrain();


        }
    }

    private void OnTriggerEnter2D(Collider2D coll)
    {
        if (coll.gameObject.CompareTag("Player"))
        {
            if (safeArea == null) playerController.TakeDamage(damage);
            else if (arenaOwner != null && arenaOwner.IsRunning) DamageAndConstrain();
           

        }
    }

}
