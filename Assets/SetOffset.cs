using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetOffset : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject frame;
    void Start()
    {
        transform.position = frame.transform.position;
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
