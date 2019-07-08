using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class ScreenUI : MonoBehaviour
{
    private Canvas canvas;
    public Text initText;
    public Image startImage;
    public static ScreenUI current { get; private set; }
    private void Awake()
    {
        if (current && current != this)
        {
            Destroy(gameObject);
            return;
        }
        canvas = GetComponent<Canvas>();
        current = this;
        DontDestroyOnLoad(gameObject);
        initText.text = string.Empty;
        initText.enabled = false;
    }
    private void OnDestroy()
    {
        if (current == this) current = null;
    }
}
