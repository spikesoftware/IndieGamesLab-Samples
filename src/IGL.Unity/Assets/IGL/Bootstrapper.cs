using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Bootstrapper : MonoBehaviour
{
    public string Bundle;
    bool _hasLoaded;
    private bool _loading = false;

	// Use this for initialization
	void Start ()
    {
        if (!string.IsNullOrEmpty(Bundle))
        {
            string platform = Application.platform.ToString();

#if UNITY_EDITOR
            platform = "WindowsAssetBundles";
#endif

            StartCoroutine(LoadAssetAzureRoutine(platform + "/" + Bundle));
        }        
	}

    void FixedUpdate()
    {
        if (_hasLoaded && !_loading)
        {            
            _loading = true;
            StartCoroutine(LoadFirstScene());
        }
    }


    public IEnumerator LoadAssetAzureRoutine(string name)
    {        
        int retry = 0;

        var url = string.Format("https://indiegameslab.blob.core.windows.net/assetbundles/{0}", name);

        do
        {
            Debug.Log("Starting to retrieve bundle....");

            WWW request = WWW.LoadFromCacheOrDownload(url, 1);
            yield return request;
            AssetBundle bundle = request.assetBundle;

            if (bundle == null)
            {
                Debug.LogErrorFormat("Failed to load AssetBundle {0}!", name);
                retry += 1;
            }
            else
            {
                Debug.Log("Bundle retrieved.");
                _hasLoaded = true;
            }
        }
        while (_hasLoaded != true && retry < 3);
    }
    IEnumerator LoadFirstScene()
    {        
        yield return null;
        Debug.Log("Loading scene....");
        yield return SceneManager.LoadSceneAsync("Assets/IGL/EchoScene.unity");
    }

}
