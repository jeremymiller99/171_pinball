/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using UnityEngine;
using UnityEngine.UIElements;

namespace TelePresent.SmartMouse
{
    /// Makes a floating Scene-View panel draggable by its title bar.
    internal sealed class SmartMousePanelDragger : Manipulator
    {
        readonly VisualElement _panel;
        Vector2 _pointerStart;
        Vector2 _panelStart;
        bool _dragging;
        int _pointerId = -1;

        public SmartMousePanelDragger(VisualElement panel) { _panel = panel; }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            target.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            FinishDrag(false);
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || _dragging) return;
            _dragging = true;
            _pointerId = evt.pointerId;
            _pointerStart = evt.position;
            _panelStart = new Vector2(_panel.layout.x, _panel.layout.y);
            target.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_dragging || evt.pointerId != _pointerId) return;

            Vector2 delta = (Vector2)evt.position - _pointerStart;
            VisualElement parent = _panel.parent;
            float maxLeft = parent != null ? Mathf.Max(0f, parent.layout.width - _panel.layout.width) : 1e5f;
            float maxTop = parent != null ? Mathf.Max(0f, parent.layout.height - _panel.layout.height) : 1e5f;

            _panel.style.left = Mathf.Clamp(_panelStart.x + delta.x, 0f, maxLeft);
            _panel.style.top = Mathf.Clamp(_panelStart.y + delta.y, 0f, maxTop);
            // Once dragged, anchor by left/top so the corner anchor it opened with does not fight the new position.
            _panel.style.bottom = StyleKeyword.Auto;
            _panel.style.right = StyleKeyword.Auto;
            evt.StopPropagation();
        }

        void OnPointerUp(PointerUpEvent evt)
        {
            if (!_dragging || evt.pointerId != _pointerId || evt.button != 0) return;
            FinishDrag(true);
            evt.StopPropagation();
        }

        void OnPointerCancel(PointerCancelEvent evt)
        {
            if (!_dragging || evt.pointerId != _pointerId) return;
            FinishDrag(true);
            evt.StopPropagation();
        }

        void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (_dragging && evt.pointerId == _pointerId)
                FinishDrag(false);
        }

        void FinishDrag(bool releaseCapture)
        {
            if (!_dragging) return;
            int pointerId = _pointerId;
            _dragging = false;
            _pointerId = -1;
            if (releaseCapture && pointerId >= 0 && target.HasPointerCapture(pointerId))
                target.ReleasePointer(pointerId);
        }
    }
}
