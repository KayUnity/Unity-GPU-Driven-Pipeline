using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuyHeart : TriggerEvent
{
    public GameObject[] disabledObjs;
    public GameObject character;
    public GameObject[] enabledObjs;
    private void Awake()
    {
        enabled = false;
    }
    public override void ExecuteTrigger()
    {
        enabled = true;
        foreach(var i in disabledObjs)
        {
            i.SetActive(false);
        }
        foreach (var i in enabledObjs)
            i.SetActive(true);
        character.SetActive(true);
    }
    private void Update()
    {
        if(!character.activeSelf)
        {
            foreach (var i in disabledObjs)
                i.SetActive(true);
            foreach (var i in enabledObjs)
                i.SetActive(false);
            Destroy(this);
        }
    }
}
