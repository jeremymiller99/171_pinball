using UnityEngine;

/// <summary>
/// Console tracing for the charge system. Filter the Console on "[Charge]" to
/// follow shocks, transfers, decay, and item triggers; flip enabled off to silence.
/// </summary>
public static class ChargeDebug
{
    public static bool enabled = true;

    public static void Log(string message)
    {
        if (enabled)
        {
            Debug.Log($"[Charge] {message}");
        }
    }
}
