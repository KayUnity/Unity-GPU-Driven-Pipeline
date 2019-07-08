using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine.UI;
public class SceneEventTrigger : MonoBehaviour
{
    public Transform bounding;
    public Renderer stopSign;
    public Transform characterCamera;
    public string signStr;
    public TriggerEvent triEvent;
    private float stop = 0;
    public bool allowedUseThis = false;
    private Text selfText;
    private void Awake()
    {
        selfText = Instantiate(ScreenUI.current.initText, ScreenUI.current.transform).GetComponent<Text>();
        selfText.enabled = false;
    }
    private void Update()
    {
        float3x4 worldToLocal = MPipeline.MathLib.GetWorldToLocal(bounding);
        float3 localPos = mul(worldToLocal, float4(characterCamera.position, 1));
        if(abs(localPos.x) < 0.5f && abs(localPos.y) < 0.5f && abs(localPos.z) < 0.5f)
        {
            selfText.enabled = true;
            selfText.text = signStr;
            if(Input.GetKeyDown(KeyCode.E))
            {
                if (allowedUseThis)
                {
                    triEvent.ExecuteTrigger();
                    stopSign.enabled = false;
                    selfText.enabled = false;
                    Destroy(this);
                }
                else
                {
                    stop = 2;
                    stopSign.enabled = true;
                }
            }
        }
        else
        {
            selfText.enabled = false;
        }
        if(stop > 0)
        {
            stop -= Time.deltaTime;
            if (stop <= 0) stopSign.enabled = false;
        }
    }
}
