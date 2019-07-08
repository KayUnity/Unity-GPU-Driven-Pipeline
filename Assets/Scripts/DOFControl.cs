using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
public class DOFControl : MonoBehaviour
{
    public PostProcessProfile profile;
    [Range(0.1f, 32f)]
    public float aperture;
    public float focusDistance;
    private DepthOfField setting;
    private void Start()
    {
        var sts = profile.settings;
        foreach(var i in sts)
        {
            if(i.GetType()== typeof(DepthOfField))
            {
                setting = i as DepthOfField;
            }
        }
    }


    private void Update()
    {
        setting.focusDistance.value = focusDistance;
        setting.aperture.value = aperture;
    }
}
