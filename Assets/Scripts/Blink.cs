using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Blink : MonoBehaviour
{

    public Transform[] eyes; 
    public float k = 1f;
    public float b = 0f;
    public float a = 2f;
    void Update()
    {
        var t =  Mathf.Clamp01(a * Mathf.Abs(Mathf.Sin(k * Time.time + b)));
        foreach (var eye in eyes)
        {
            eye.localScale = new Vector3(1f, t, 1f);
        }
    }
}
