using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookAtCam : MonoBehaviour
{
    [SerializeField]
    Camera cam;

    private void Awake()
    {
        cam = Camera.main;
    }

    // Update is called once per frame
    private void Update()
    {
        Vector3 toCam = cam.transform.position - transform.position;
        toCam.y = 0;
        transform.rotation = Quaternion.LookRotation(-toCam, Vector3.up);
        transform.position = transform.parent.position + Vector3.up * 0.85f;
    }
}
