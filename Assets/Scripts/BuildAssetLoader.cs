using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Loads project assets in both the Editor and standalone builds. Editor lookups go through
/// AssetDatabase (exact project paths); players fall back to mirrored copies under
/// Assets/Resources/BuildAssets so the same asset path works in a build.
/// </summary>
public static class BuildAssetLoader
{
    private const string AssetsPrefix = "Assets/";
    private const string ResourceRoot = "BuildAssets/";

    public static T Load<T>(string assetPath) where T : Object
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return null;
        }

#if UNITY_EDITOR
        T editorAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (editorAsset != null)
        {
            return editorAsset;
        }
#endif

        string resourcePath = ToResourcePath(assetPath);
        return resourcePath == null ? null : Resources.Load<T>(resourcePath);
    }

    public static T[] LoadAll<T>(string assetPath) where T : Object
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return new T[0];
        }

#if UNITY_EDITOR
        var found = new List<T>();
        foreach (Object asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (asset is T match)
            {
                found.Add(match);
            }
        }

        if (found.Count > 0)
        {
            return found.ToArray();
        }
#endif

        string resourcePath = ToResourcePath(assetPath);
        return resourcePath == null ? new T[0] : Resources.LoadAll<T>(resourcePath);
    }

    private static string ToResourcePath(string assetPath)
    {
        if (!assetPath.StartsWith(AssetsPrefix, System.StringComparison.Ordinal))
        {
            return null;
        }

        string relative = assetPath.Substring(AssetsPrefix.Length);
        string extension = Path.GetExtension(relative);
        if (!string.IsNullOrEmpty(extension))
        {
            relative = relative.Substring(0, relative.Length - extension.Length);
        }

        return ResourceRoot + relative;
    }
}
