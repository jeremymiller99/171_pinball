// Generated with Cursor (GPT-5.2) by OpenAI assistant for jjmil on 2026-02-19.
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BallHandSlotMarker : MonoBehaviour
{
    [SerializeField] private int slotIndex = -1;

    public int SlotIndex => slotIndex;

    public void SetSlotIndex(int index)
    {
        slotIndex = index;
    }
}

