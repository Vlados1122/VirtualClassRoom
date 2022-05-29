using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeFieldOfVew : MonoBehaviour
{
    public Camera cam;
    private float defaultFov;

    [SerializeField] private float minFov = 15f;
    [SerializeField] private float maxFov = 90f;
    [SerializeField] private float sensitivity = 10f;
    
    private void Start()
    {
        cam = GetComponent<Camera>();
        defaultFov = cam.fieldOfView;
        //minFov = defaultFov;
    }

    void Update()
    {
        //if (Input.GetMouseButton(2))
        //{
        //    cam.fieldOfView = (defaultFov / 3);
        //}
        //else
        //{
        //    cam.fieldOfView = (defaultFov);
        //}

        float fov = cam.fieldOfView;
        fov -= Input.GetAxis("Mouse ScrollWheel") * sensitivity;
        fov = Mathf.Clamp(fov, minFov, maxFov);
        cam.fieldOfView = fov;
    }
}
