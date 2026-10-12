using System.IO;
using UnityEditor;

public static class LabSourceSync
{
    [MenuItem("Tools/Unity Learning/Sync Lab Source")]
    public static void Sync()
    {
        const string destination = "Assets/Resources/Learning/Labs/Source";
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles("Assets/Scripts/Learning/Labs", "*.cs"))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file) + ".txt"), true);
        File.Copy("Assets/Editor/Labs/LabContentBuilder.cs", Path.Combine(destination, "LabContentBuilder.cs.txt"), true);
        AssetDatabase.Refresh();
    }
}
