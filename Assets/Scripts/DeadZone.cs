using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeadZone : MonoBehaviour
{
 
   
    public float damage = 60;


    private PlayerController playerController;
   

    private void Start()
    { 
        playerController = FindObjectOfType<PlayerController>();
    }



    private void OnTriggerStay2D(Collider2D coll)
    {
        if (coll.gameObject.CompareTag("Player"))
        {
            playerController.TakeDamage(damage);


        }
    }

    private void OnTriggerEnter2D(Collider2D coll)
    {
        if (coll.gameObject.CompareTag("Player"))
        {
            playerController.TakeDamage(damage);
           

        }
    }

}