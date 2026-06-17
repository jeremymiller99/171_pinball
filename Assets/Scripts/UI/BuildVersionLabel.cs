using TMPro;
using UnityEngine;

/// <summary>
/// Stamps the build number onto a TMP label at runtime.
/// CI (GitHub Actions) writes the run number into <see cref="Application.version"/>
/// at build time; in the editor it shows whatever bundle version is set in
/// Project Settings.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class BuildVersionLabel : MonoBehaviour
{
    [Tooltip("Use {0} as the placeholder for the build/version number.")]
    [SerializeField] private string format = "Build #{0}";

    [Tooltip("Shown in the editor / local play, where there is no CI build number.")]
    [SerializeField] private string editorLabel = "Build #dev";

    private void Awake()
    {
        GetComponent<TMP_Text>().text = Application.isEditor
            ? editorLabel
            : string.Format(format, Application.version);
    }
}
