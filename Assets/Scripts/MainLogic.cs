using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using MPipeline;
using static Unity.Mathematics.math;
using UnityEngine.UI;

public unsafe sealed class MainLogic : MonoBehaviour
{
    [Header("First Scene:")]
    [SerializeField] private GameObject characControl;
    [SerializeField] private GameObject startUICamera;
    [SerializeField] private GameObject effectsCamera;
    [SerializeField] private Vector3 firstScene_CharacterInitPosition;
    [SerializeField] private Quaternion firstScene_CharacterInitRotation;
    [SerializeField] private Transform buyHeartBoxTrigger;
    [SerializeField] private Transform getDoorKeyTrigger;
    private Text selfText;
    private void Awake()
    {
        startUICamera.SetActive(true);
        characControl.SetActive(true);
        effectsCamera.SetActive(true);
        selfText = Instantiate(ScreenUI.current.initText, ScreenUI.current.transform).GetComponent<Text>();
        selfText.enabled = false;
    }
    [EasyButtons.Button]
    private void InitFirstScenePos()
    {
        firstScene_CharacterInitPosition = characControl.transform.position;
        firstScene_CharacterInitRotation = characControl.transform.rotation;
    }

    public void StartScene()
    {
        selfText.enabled = true;
        selfText.text = "Press Space To Start";
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ToEffectsCamera();
        }
    }
    public void ToEffectsCamera()
    {
        ScreenUI.current.startImage.enabled = false;
        startUICamera.SetActive(false);
        characControl.SetActive(false);
        effectsCamera.SetActive(true);
    }

    public void ToFirstScene()
    {
        characControl.transform.position = firstScene_CharacterInitPosition;
        characControl.transform.rotation = firstScene_CharacterInitRotation;
        characControl.SetActive(true);
        effectsCamera.SetActive(false);
        selfText.enabled = false;
        Destroy(this);
    }
}
