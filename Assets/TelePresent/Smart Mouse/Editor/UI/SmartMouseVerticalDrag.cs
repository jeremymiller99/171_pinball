/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TelePresent.SmartMouse
{
    internal sealed class SmartMouseVerticalDrag : VisualElement
    {
        public float LowValue;
        public float HighValue;

        const float DefaultThumbHeight = 14f;

        readonly VisualElement _thumb;
        float _value;
        bool _dragging;
        bool _resetGesture;
        int _pointerId = -1;

        public event Action DragStarted;
        public event Action DragEnded;
        public event Action<float, float> ValueChanged;

        public event Action DoubleClicked;

        public float Value => _value;

        public SmartMouseVerticalDrag(float low, float high)
        {
            LowValue = low;
            HighValue = high;

            AddToClassList("sm-vdrag");

            _thumb = new VisualElement();
            _thumb.AddToClassList("sm-vdrag-thumb");
            _thumb.pickingMode = PickingMode.Ignore;
            Add(_thumb);

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            RegisterCallback<DetachFromPanelEvent>(_ => FinishDrag(false));
            RegisterCallback<GeometryChangedEvent>(_ => UpdateThumb());
        }

        public void SetValueWithoutNotify(float value)
        {
            _value = Clamp(value);
            UpdateThumb();
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || _dragging) return;
            _dragging = true;
            _pointerId = evt.pointerId;
            this.CapturePointer(evt.pointerId);
            DragStarted?.Invoke();
            _resetGesture = evt.clickCount >= 2 && DoubleClicked != null;
            if (_resetGesture)
                DoubleClicked.Invoke();
            else
                ApplyFromPointer(evt.localPosition.y);
            evt.StopPropagation();
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_dragging || _resetGesture || evt.pointerId != _pointerId) return;
            ApplyFromPointer(evt.localPosition.y);
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
            _resetGesture = false;
            _pointerId = -1;
            if (releaseCapture && pointerId >= 0 && this.HasPointerCapture(pointerId))
                this.ReleasePointer(pointerId);
            DragEnded?.Invoke();
        }

        void ApplyFromPointer(float localY)
        {
            float h = TrackHeight();
            if (h <= 1f) return;

            float t = Mathf.Clamp01(localY / h);
            float next = Mathf.Lerp(HighValue, LowValue, t); 
            float previous = _value;
            _value = next;
            UpdateThumb();
            if (!Mathf.Approximately(next, previous)) ValueChanged?.Invoke(next, previous);
        }

        void UpdateThumb()
        {
            float h = TrackHeight();
            if (h <= 1f) return;

            float thumbH = _thumb.resolvedStyle.height;
            if (thumbH <= 0f) thumbH = DefaultThumbHeight;

            float t = Mathf.InverseLerp(HighValue, LowValue, _value);
            float y = Mathf.Clamp(t * h - thumbH * 0.5f, 0f, Mathf.Max(0f, h - thumbH));
            _thumb.style.top = y;
        }

        float TrackHeight()
        {
            float h = resolvedStyle.height;
            return h > 1f ? h : layout.height;
        }

        float Clamp(float value) => Mathf.Clamp(value, Mathf.Min(LowValue, HighValue), Mathf.Max(LowValue, HighValue));
    }
}
