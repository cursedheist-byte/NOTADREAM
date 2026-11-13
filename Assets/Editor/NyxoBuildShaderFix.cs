using UnityEditor;
using UnityEngine;

public static class NyxoBuildShaderFix
{
    private static readonly string[] ShaderNames =
    {
        "Hidden/Universal Render Pipeline/GaussianDepthOfField",
        "Hidden/Universal Render Pipeline/BokehDepthOfField",
        "Hidden/Universal Render Pipeline/PaniniProjection"
    };

    public static void Apply()
    {
        Object graphicsSettings = null;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset"))
        {
            graphicsSettings = asset;
            break;
        }

        if (graphicsSettings == null)
        {
            Debug.LogError("NyxoBuildShaderFix: GraphicsSettings.asset could not be loaded.");
            return;
        }

        var settings = new SerializedObject(graphicsSettings);
        SerializedProperty alwaysIncluded = settings.FindProperty("m_AlwaysIncludedShaders");
        if (alwaysIncluded == null || !alwaysIncluded.isArray)
        {
            Debug.LogError("NyxoBuildShaderFix: m_AlwaysIncludedShaders not found.");
            return;
        }

        foreach (string shaderName in ShaderNames)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("NyxoBuildShaderFix: shader not found: " + shaderName);
                continue;
            }

            bool present = false;
            for (int i = 0; i < alwaysIncluded.arraySize; i++)
            {
                if (alwaysIncluded.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                {
                    present = true;
                    break;
                }
            }

            if (present)
            {
                Debug.Log("NyxoBuildShaderFix: already included " + shaderName);
                continue;
            }

            alwaysIncluded.InsertArrayElementAtIndex(alwaysIncluded.arraySize);
            alwaysIncluded.GetArrayElementAtIndex(alwaysIncluded.arraySize - 1).objectReferenceValue = shader;
            Debug.Log("NyxoBuildShaderFix: added " + shaderName);
        }

        settings.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Debug.Log("NyxoBuildShaderFix: done, alwaysIncluded count=" + alwaysIncluded.arraySize);
    }
}
