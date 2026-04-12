using UnityEngine;

/// <summary>
/// Marks a GameObject as one inventory slot in the player's hand.
/// Placed in the scene with a (large) BoxCollider that serves as the drop/hover target
/// for shop drag-and-drop interactions. Its Transform position is where a ball in that
/// slot is drawn. Slot index is assigned at runtime by <see cref="BallSpawner"/> based on
/// its position in the spawner's serialized slot list.
/// </summary>
[DisallowMultipleComponent]
public sealed class BallHandSlot : MonoBehaviour
{
    public int SlotIndex { get; private set; } = -1;

    public void AssignSlotIndex(int index)
    {
        SlotIndex = index;
    }
}
