using UnityEngine;

/// <summary>
/// Console tracing for the fire system. Filter the Console on "[Fire]" to
/// follow fuel, ignitions, and item triggers; flip enabled off to silence.
/// </summary>
public static class FireDebug
{
    public static bool enabled = true;

    public static void Log(string message)
    {
        if (enabled)
        {
            Debug.Log($"[Fire] {message}");
        }
    }
}
