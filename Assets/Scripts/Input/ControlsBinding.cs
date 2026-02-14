[System.Serializable]
public struct ControlsBinding
{
    /// <summary>
    /// Keyboard key name (serialized as string so it works with either Input System Key or legacy KeyCode).
    /// Examples: "RightArrow", "Space", "" (none).
    /// </summary>
    public string key;

    /// <summary>
    /// -1 = none, 0 = left, 1 = right, 2 = middle.
    /// </summary>
    public int mouseButton;

    public static ControlsBinding None => new ControlsBinding { key = "", mouseButton = -1 };

    public static ControlsBinding FromKey(string keyName)
    {
        return new ControlsBinding { key = keyName ?? "", mouseButton = -1 };
    }

    public static ControlsBinding FromMouse(int mouseButton)
    {
        return new ControlsBinding { key = "", mouseButton = mouseButton };
    }

    public string ToDisplayString()
    {
        string keyPart = string.IsNullOrWhiteSpace(key) ? "" : key;
        string mousePart = MouseButtonToString(mouseButton);

        if (!string.IsNullOrWhiteSpace(keyPart) && !string.IsNullOrWhiteSpace(mousePart))
            return $"{keyPart} + {mousePart}";
        if (!string.IsNullOrWhiteSpace(keyPart))
            return keyPart;
        if (!string.IsNullOrWhiteSpace(mousePart))
            return mousePart;
        return "Unbound";
    }

    private static string MouseButtonToString(int btn)
    {
        switch (btn)
        {
            case 0: return "Mouse Left";
            case 1: return "Mouse Right";
            case 2: return "Mouse Middle";
            default: return "";
        }
    }
}

