using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Bootstrapper : MonoBehaviour
{
    public string[] Bundles;
    public string FirstScene;

    public Text StatusText;

    private Dictionary<string, AssetBundle> _bundles;

    private bool _loadingFirstScene = false;

    // Use this for initialization
    void Start()
    {
        DontDestroyOnLoad(gameObject);

        _bundles = new Dictionary<string, AssetBundle>();

        LoadAssetAzure();
    }


    private void FixedUpdate()
    {
        // if all the bundles have been downloaded
        if (!_loadingFirstScene && _bundles.Count == Bundles.Length)
        {
            _loadingFirstScene = true;
            StartCoroutine(LoadFirstScene());
        }
    }

    IEnumerator LoadFirstScene()
    {
        yield return null;        
        yield return SceneManager.LoadSceneAsync(FirstScene);        
    }

    public void LoadAssetAzure()
    {
        StatusText.text += "Retrieving latest bundles..." + Environment.NewLine;

        string platform = Application.platform.ToString();

#if UNITY_EDITOR
        platform = "WindowsAssetBundles";
#endif

        foreach (var name in Bundles)
        {
            StartCoroutine(LoadAssetAzureRoutine(platform + "/" + name));
        }
    }

    public IEnumerator LoadAssetAzureRoutine(string name)
    {
        bool hasLoaded = false;
        int retry = 0;

        var url = string.Format("https://indiegameslab.blob.core.windows.net/assetbundles/{0}", name);

        do
        {
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
                


                _bundles.Add(bundle.name, bundle);
                hasLoaded = true;
            }
        }
        while (hasLoaded != true && retry < 3);
    }
}