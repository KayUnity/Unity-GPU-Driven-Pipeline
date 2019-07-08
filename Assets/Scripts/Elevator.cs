using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine.UI;
public class Elevator : MonoBehaviour
{
    private bool runUpdate = true;
    public Transform characCamera;
    private Text selfText;
    public Transform leftDoor;
    public Transform rightDoor;
    public Vector3 leftDoorTargetPos;
    public Vector3 rightDoorTargetPos;
    [EasyButtons.Button]
    void SetPos()
    {
        leftDoorTargetPos = leftDoor.position;
        rightDoorTargetPos = rightDoor.position;
    }
    private void Start()
    {
        selfText = Instantiate(ScreenUI.current.initText, ScreenUI.current.transform).GetComponent<Text>();
        selfText.enabled = false;
    }
    private IEnumerator ElevatorLerp()
    {
        while(true)
        {
            leftDoor.position = lerp(leftDoor.position, leftDoorTargetPos, Time.deltaTime * 8f);
            rightDoor.position = lerp(rightDoor.position, rightDoorTargetPos, Time.deltaTime * 8f);
            yield return null;
        }
    }
    private IEnumerator ElevatorTest()
    {
        selfText.enabled = false;
        yield return new WaitForSeconds(2);
        selfText.enabled = false;
        MPipeline.SceneController.TransformScene(0);
        UnityEngine.SceneManagement.SceneManager.LoadScene(1);
    }
    private void Update()
    {
        if (!runUpdate) return;
        float3x4 worldToLocal = MPipeline.MathLib.GetWorldToLocal(transform);
        float3 localPos = mul(worldToLocal, float4(characCamera.position, 1));
        if (abs(localPos.x) < 0.5f && abs(localPos.y) < 0.5f && abs(localPos.z) < 0.5f)
        {
            selfText.enabled = true;
            selfText.text = "Press E to go";
            if (Input.GetKeyDown(KeyCode.E))
            {
                StartCoroutine(ElevatorLerp());
                StartCoroutine(ElevatorTest());
                runUpdate = false;
            }
        }
        else
        {
            selfText.enabled = false;
        }
    }
}
