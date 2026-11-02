using System;
using UnityEngine;
using UnityEngine.InputSystem;

public static class WorldMasterKeyBindings
{
    private const string PreferencePrefix = "WorldMaster.KeyBinding.";

    private static readonly string[] BindingNames =
    {
        "Forward",
        "Backward",
        "Left",
        "Right",
        "Jump",
        "Sprint",
        "Interact"
    };

    public static event Action<string, Key> BindingChanged;

    public static Key GetKey(string bindingName)
    {
        Key defaultKey = GetDefaultKey(bindingName);
        int keyValue = PlayerPrefs.GetInt(PreferencePrefix + bindingName, (int)defaultKey);
        Key key = (Key)keyValue;
        return Enum.IsDefined(typeof(Key), key) && key != Key.None ? key : defaultKey;
    }

    public static string GetPath(string bindingName)
    {
        return "<Keyboard>/" + GetKey(bindingName).ToString().ToLowerInvariant();
    }

    public static bool TrySetKey(string bindingName, Key key)
    {
        if (!IsKnownBinding(bindingName) || key == Key.None || key == Key.Escape)
            return false;

        foreach (string otherBinding in BindingNames)
        {
            if (otherBinding != bindingName && GetKey(otherBinding) == key)
                return false;
        }

        PlayerPrefs.SetInt(PreferencePrefix + bindingName, (int)key);
        PlayerPrefs.Save();
        BindingChanged?.Invoke(bindingName, key);
        return true;
    }

    public static void ResetToDefaults()
    {
        foreach (string bindingName in BindingNames)
        {
            PlayerPrefs.DeleteKey(PreferencePrefix + bindingName);
            BindingChanged?.Invoke(bindingName, GetDefaultKey(bindingName));
        }

        PlayerPrefs.Save();
    }

    private static bool IsKnownBinding(string bindingName)
    {
        foreach (string knownBinding in BindingNames)
        {
            if (knownBinding == bindingName)
                return true;
        }

        return false;
    }

    private static Key GetDefaultKey(string bindingName)
    {
        switch (bindingName)
        {
            case "Forward": return Key.W;
            case "Backward": return Key.S;
            case "Left": return Key.A;
            case "Right": return Key.D;
            case "Jump": return Key.Space;
            case "Sprint": return Key.LeftShift;
            case "Interact": return Key.E;
            default: throw new ArgumentException("Unknown key binding: " + bindingName, nameof(bindingName));
        }
    }
}
