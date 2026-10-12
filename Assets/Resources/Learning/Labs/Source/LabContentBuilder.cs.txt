using System.IO;
using UnityEditor;
using UnityEngine;
using UnityLearning.Labs;

public static class LabContentBuilder
{
    [MenuItem("Tools/Unity Learning/Build Lab Content")]
    public static void Build()
    {
        const string source = "Assets/LearningLabContent/Source";
        string output = Path.Combine(Application.streamingAssetsPath, "LearningLabPackages");
        Directory.CreateDirectory(source); Directory.CreateDirectory(output); AssetDatabase.Refresh();
        var definitions = new AssetBundleBuild[2];
        for (int i = 0; i < 2; i++)
        {
            int version = i + 1;
            string materialPath = source + "/V" + version + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                material.color = i == 0 ? new Color(.95f, .4f, .25f) : new Color(.25f, .65f, .95f);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = "Reward V" + version;
            cube.transform.localScale = Vector3.one * (i == 0 ? 2 : 3);
            cube.GetComponent<Renderer>().sharedMaterial = material;
            string prefab = source + "/RewardV" + version + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(cube, prefab); Object.DestroyImmediate(cube);
            definitions[i] = new AssetBundleBuild { assetBundleName = "learning_v" + version, assetNames = new[] { prefab } };
        }
        AssetDatabase.SaveAssets();
        var manifest = BuildPipeline.BuildAssetBundles(output, definitions,
            BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle,
            BuildTarget.StandaloneWindows64);
        if (manifest == null) throw new System.InvalidOperationException("AssetBundle 构建失败");
        var config = new LabPackageManifest { platform = "StandaloneWindows64", versions = new LabPackageVersion[2] };
        for (int i = 0; i < 2; i++)
        {
            if (!BuildPipeline.GetCRCForAssetBundle(Path.Combine(output, definitions[i].assetBundleName), out uint crc))
                throw new System.InvalidOperationException("无法读取 CRC");
            config.versions[i] = new LabPackageVersion { version = i + 1,
                file = definitions[i].assetBundleName, hash = manifest.GetAssetBundleHash(definitions[i].assetBundleName).ToString(),
                crc = crc, asset = definitions[i].assetNames[0] };
        }
        File.WriteAllText(Path.Combine(output, "manifest.json"), JsonUtility.ToJson(config, true));
        AssetDatabase.Refresh(); Debug.Log("Lab AssetBundles ready: " + output);
    }
}
