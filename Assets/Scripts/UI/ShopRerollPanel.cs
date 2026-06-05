using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Board-side world-space panel that exposes the shop's Reroll and Done
/// buttons. The board scene (e.g. Board_NA) is loaded additively, so the
/// UnifiedShopController in the core scene cannot hold direct inspector
/// references to these buttons. Instead this component registers itself with
/// the ServiceLocator; the controller looks it up on shop open, wires the
/// buttons to its public actions, and toggles visibility.
/// </summary>
[DisallowMultipleComponent]
public sealed class ShopRerollPanel : MonoBehaviour
{
    [Tooltip("Root toggled for visibility. Defaults to this GameObject.")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button rerollButton;
    [SerializeField] private Button doneButton;

    public Button RerollButton => rerollButton;
    public Button DoneButton => doneButton;

    private void Awake()
    {
        if (panelRoot == null) panelRoot = gameObject;
        ServiceLocator.Register<ShopRerollPanel>(this);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<ShopRerollPanel>();
    }

    private void Start()
    {
        // Hidden until the shop opens and the controller calls Show().
        Hide();
    }

    public void Show()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }
}
