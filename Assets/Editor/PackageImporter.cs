using UnityEngine;
using UnityEditor;
using System.IO;

public static class PackageImporter
{
    static bool _doneFired;

    public static void ImportHospitalPack()
    {
        const string pkg = "C:/Users/hvnes/YomawariByoin/hospital_pack.unitypackage";
        if (!File.Exists(pkg))
        {
            Debug.LogError($"Package not found: {pkg}");
            EditorApplication.Exit(2);
            return;
        }

        AssetDatabase.importPackageCompleted += name =>
        {
            if (_doneFired) return;
            _doneFired = true;
            Debug.Log($"importPackageCompleted: {name}");
            EditorApplication.update += FinishAfterRefresh;
        };
        AssetDatabase.importPackageFailed += (name, err) =>
        {
            Debug.LogError($"importPackageFailed: {name} - {err}");
            EditorApplication.Exit(1);
        };
        AssetDatabase.importPackageCancelled += name =>
        {
            Debug.LogError($"importPackageCancelled: {name}");
            EditorApplication.Exit(1);
        };

        Debug.Log($"Starting import: {pkg}");
        AssetDatabase.ImportPackage(pkg, false);
        // Do NOT exit here - the callback will exit.
    }

    static int _refreshFrames = 0;
    static void FinishAfterRefresh()
    {
        _refreshFrames++;
        if (_refreshFrames < 30) return;  // give a few frames for asset refresh
        EditorApplication.update -= FinishAfterRefresh;
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        Debug.Log("Import complete. Exiting.");
        EditorApplication.Exit(0);
    }
}
