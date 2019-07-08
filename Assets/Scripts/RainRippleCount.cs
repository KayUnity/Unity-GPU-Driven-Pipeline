using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RainRippleCount : MonoBehaviour
{
    public int updateFPS = 30;
    public int tile = 8;
    private Vector2 currentOffset = Vector2.zero;
    private float timeCount = 0;
    private static readonly int _RainRippleScaleOffset = Shader.PropertyToID("_RainRippleScaleOffset");
    void Update()
    {
        float frame = 1f / updateFPS;
        float tileSize = 1f / tile;
        if (timeCount > frame)
        {
            timeCount -= frame;
            currentOffset.x += tileSize;
            if(currentOffset.x >= 0.9999f)
            {
                currentOffset.y += tileSize;
                currentOffset.x = 0;
                if(currentOffset.y >= 0.9999f)
                {
                    currentOffset = Vector2.zero;
                }
            }
          //  Debug.Log(currentOffset);
            Shader.SetGlobalVector(_RainRippleScaleOffset, new Vector4(tileSize, tileSize, currentOffset.x,currentOffset.y));
        }
        timeCount += Time.deltaTime;
    }
}
