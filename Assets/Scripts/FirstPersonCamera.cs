using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FirstPersonCamera : MonoBehaviour
{
    public Vector3 initLocalPosition;
    public float lookSpeed = 5f;
    float m_yaw;
    float m_pitch;
    
    void CaptureInput()
    {
        //按下后隐藏鼠标
        Cursor.lockState = CursorLockMode.Locked;
        //Cursor.lockState = CursorLockMode.None;


        Cursor.visible = false;

        m_yaw = transform.eulerAngles.y;
        //m_yaw = Input.mousePosition.y;
        m_pitch = transform.eulerAngles.x;
        //m_pitch = Input.mousePosition.x;
    }


    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void LateUpdate()
    {
        CaptureInput();
        transform.localPosition = initLocalPosition;
        var rotStrafe = Input.GetAxis("Mouse X");
        var rotFwd = Input.GetAxis("Mouse Y");

        m_yaw = (m_yaw + lookSpeed * rotStrafe) % 360f;
        m_pitch = (m_pitch - lookSpeed * rotFwd) % 360f;
        transform.rotation = Quaternion.AngleAxis(m_yaw, Vector3.up);// * Quaternion.AngleAxis(m_pitch, Vector3.right);
        if (m_pitch > 180) m_pitch -= 360;
        if (m_pitch < -180) m_pitch += 360;
        m_pitch = Mathf.Clamp(m_pitch, -85, 85);
        Vector3 euler = transform.eulerAngles;
        euler.x = m_pitch;
        transform.eulerAngles = euler;
    }
}
