using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Luja : MonoBehaviour
{
    public float lifeTime = 8f;
    // Start is called before the first frame update


    private void OnDestroy()
    {
        Destroy(gameObject);
    }
    // Update is called once per frame
    void Update()
    {
        Destroy(gameObject, lifeTime);
    }
}
