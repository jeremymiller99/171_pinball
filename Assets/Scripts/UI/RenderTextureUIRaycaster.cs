// Created with Claude Code (Opus 4.8) by JJ on 2026-06-03: pointer remap for world-space UI filmed into a render texture.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Drop-in replacement for <see cref="GraphicRaycaster"/> on a World Space
/// canvas whose owning camera renders into a RenderTexture that is then shown
/// full-screen (e.g. the CRT monitor effect).
///
/// The pointer arrives in screen pixels (0..Screen.width), but the canvas's
/// Event Camera renders into a lower-resolution RenderTexture and therefore
/// reasons in RT pixels (0..RT.width). This raycaster rescales the pointer into
/// the camera's pixel space before raycasting, then restores it so other
/// canvases keep working normally.
///
/// Requirements:
///  * The canvas's "Event Camera" must be set to the camera that renders the RT.
///  * The texture must be displayed full-screen with no letterboxing
///    (a RawImage stretched to the whole screen).
/// </summary>
[RequireComponent(typeof(Canvas))]
public sealed class RenderTextureUIRaycaster : GraphicRaycaster
{
    public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
    {
        Camera cam = eventCamera;

        // Only remap when the event camera is actually rendering into a texture.
        // Otherwise behave exactly like a normal GraphicRaycaster.
        if (cam == null || cam.targetTexture == null)
        {
            base.Raycast(eventData, resultAppendList);
            return;
        }

        Vector2 originalPosition = eventData.position;

        float normalizedX = originalPosition.x / Screen.width;
        float normalizedY = originalPosition.y / Screen.height;

        // cam.pixelWidth/Height equal the RenderTexture dimensions while a
        // target texture is assigned, so this stays correct even if the RT is
        // resized at runtime (pixelation settings, resolution changes, etc.).
        eventData.position = new Vector2(
            normalizedX * cam.pixelWidth,
            normalizedY * cam.pixelHeight);

        base.Raycast(eventData, resultAppendList);

        eventData.position = originalPosition;
    }
}
