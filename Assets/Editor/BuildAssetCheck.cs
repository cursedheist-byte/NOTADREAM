using System;
using System.Collections.Generic;
using UnityEngine;

public static class BuildAssetCheck
{
    private sealed class Entry
    {
        public string Path;
        public Type Type;
        public bool ExpectMany;
    }

    public static void Run()
    {
        string[] models =
        {
            "BuildAssets/Characters/MOM/mom",
            "BuildAssets/Characters/FRED/fred",
            "BuildAssets/Characters/MONSTER 1/kitty_-_backrooms_entity",
            "BuildAssets/Characters/MONSTER 2/the_backrooms-entity_3_smiler",
            "BuildAssets/doctor/doctor_-_sketchfab_weekly_-_13_mar23",
            "BuildAssets/Characters/LIL_BROTHER/lil bro"
        };

        string[] materials =
        {
            "BuildAssets/Characters/LIL_BROTHER/LittleBrotherVertexColors",
            "BuildAssets/Settings/Night_DarkBlueSky",
            "BuildAssets/Fantasy Skybox FREE/Panoramics/FS013/FS013_Night",
            "BuildAssets/Fantasy Skybox FREE/Panoramics/FS013/FS013_Sunset"
        };

        string[] clips =
        {
            "BuildAssets/Sounds/Walking",
            "BuildAssets/Sounds/click",
            "BuildAssets/Sounds/fred hit",
            "BuildAssets/Sounds/heartbeat",
            "BuildAssets/Sounds/horror ambiance",
            "BuildAssets/Sounds/horror hit",
            "BuildAssets/Sounds/jump",
            "BuildAssets/Sounds/kill",
            "BuildAssets/Sounds/liminal space ambiance",
            "BuildAssets/Sounds/neighbourhood ambiance",
            "BuildAssets/Sounds/suspicious ambiance",
            "BuildAssets/Sounds/wake-up-to-reality",
            "BuildAssets/SFX/NOTIFICATION",
            "BuildAssets/SFX/TEXT ANIMATION",
            "BuildAssets/UNKNOWN MAN VOICE/HEY-HOW-DID-YOU-GET-IN-THERE",
            "BuildAssets/UNKNOWN MAN VOICE/NOTHING",
            "BuildAssets/UNKNOWN MAN VOICE/OH,-YOU-DON'T-HAVE-TO-KNOW-THIS.-ITS-A-SETUP-OF",
            "BuildAssets/UNKNOWN MAN VOICE/OK,-BYE",
            "BuildAssets/UNKNOWN MAN VOICE/YEAH-YOU-CAN'T-SEE-ME-I-AM-YOUR-DEVELOPER"
        };

        string[] images =
        {
            "BuildAssets/StoryImages/image 1",
            "BuildAssets/StoryImages/image 2",
            "BuildAssets/StoryImages/image 3",
            "BuildAssets/StoryImages/image 4",
            "BuildAssets/StoryImages/image 5",
            "BuildAssets/StoryImages/image 6",
            "BuildAssets/StoryImages/image 7",
            "BuildAssets/StoryImages/fred_darkweb"
        };

        string[] officeModels =
        {
            "BuildAssets/office setup/Office Work Space/model_0",
            "BuildAssets/office setup/Office Work Space/model_1",
            "BuildAssets/office setup/Office Work Space/model_2",
            "BuildAssets/office setup/Office Work Space/model_3",
            "BuildAssets/office setup/Office Work Space/model_4",
            "BuildAssets/office setup/Office Work Space/model_5",
            "BuildAssets/office setup/Office Work Space/model_6"
        };

        var entries = new List<Entry>();
        foreach (var p in models) entries.Add(new Entry { Path = p, Type = typeof(GameObject) });
        foreach (var p in officeModels) entries.Add(new Entry { Path = p, Type = typeof(GameObject) });
        foreach (var p in materials) entries.Add(new Entry { Path = p, Type = typeof(Material) });
        foreach (var p in clips) entries.Add(new Entry { Path = p, Type = typeof(AudioClip) });
        foreach (var p in images) entries.Add(new Entry { Path = p, Type = typeof(Texture2D) });
        entries.Add(new Entry { Path = "BuildAssets/Characters/LIL_BROTHER/lil bro.obj", Type = typeof(TextAsset) });
        entries.Add(new Entry { Path = "BuildAssets/doctor/doctor_-_sketchfab_weekly_-_13_mar23", Type = typeof(AnimationClip), ExpectMany = true });

        int failed = 0;
        foreach (var entry in entries)
        {
            bool ok;
            if (entry.ExpectMany)
            {
                var many = Resources.LoadAll(entry.Path, entry.Type);
                ok = many != null && many.Length > 0;
            }
            else
            {
                ok = Resources.Load(entry.Path, entry.Type) != null;
            }

            if (!ok) failed++;
            Debug.Log((ok ? "ASSET OK   " : "ASSET FAIL ") + entry.Type.Name + " :: " + entry.Path);
        }

        Debug.Log("ASSET CHECK RESULT failures=" + failed + " of " + entries.Count);
        if (Application.isBatchMode)
        {
            UnityEditor.EditorApplication.Exit(failed == 0 ? 0 : 1);
        }
    }
}
