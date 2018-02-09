using System.IO;
using UnityEditor;


public class CreateAssetBundles
{
    [MenuItem("Assets/Build AssetBundles/Windows64")]
    static void BuildAllWindows64AssetBundles()
    {
        string assetBundleDirectory = "Assets/AssetBundles/WindowsAssetBundles";
        if (!Directory.Exists(assetBundleDirectory))
        {
            Directory.CreateDirectory(assetBundleDirectory);
        }
        BuildPipeline.BuildAssetBundles(assetBundleDirectory, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
    }

    [MenuItem("Assets/Build AssetBundles/Android")]
    static void BuildAllAndroidAssetBundles()
    {
        string assetBundleDirectory = "Assets/AssetBundles/AndroidAssetBundles";
        if (!Directory.Exists(assetBundleDirectory))
        {
            Directory.CreateDirectory(assetBundleDirectory);
        }
        BuildPipeline.BuildAssetBundles(assetBundleDirectory, BuildAssetBundleOptions.None, BuildTarget.Android);
    }
}