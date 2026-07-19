using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Absorbs pointer clicks so they stop bubbling to an ancestor.
///
/// Sits on the mission panel's window: the panel root closes on any click that
/// reaches it, which is how "click outside to dismiss" works — but without this,
/// clicks on the window's own background would bubble up and dismiss it too.
/// </summary>
public class StarMapClickSwallow : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData) { }
}
