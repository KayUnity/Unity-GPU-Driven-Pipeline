using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace MPipeline
{
    public class SceneTransformer : MonoBehaviour
    {
        private static SceneTransformer current;
        private void Awake()
        {
            if (!current)
            {
                current = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (current != this)
            {
                Destroy(gameObject);
            }
        }
        public IEnumerator Load()
        {
            yield return null;
            yield return null;
            SceneController.TransformScene(0);
        }
        private void Start()
        {
            StartCoroutine(Load());
        }
        public void LoadScene(uint v)
        {
            SceneController.TransformScene(v);
            SceneManager.LoadScene((int)v);
        }

        private void OnDestroy()
        {
            current = null;
        }
    }
}
