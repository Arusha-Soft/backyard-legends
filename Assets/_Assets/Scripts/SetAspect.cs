using UnityEngine;

public class SetAspect : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        GetComponent<Camera>().aspect = 9 / 20f;
    }
 
}
