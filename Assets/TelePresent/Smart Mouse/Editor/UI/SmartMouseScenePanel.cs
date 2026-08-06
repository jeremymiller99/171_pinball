/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TelePresent.SmartMouse
{
    /// Host for the floating Scene-View popups (Paste, Variate, Replace)
    internal sealed class SmartMouseScenePanel
    {
        public enum Corner { TopLeft, BottomLeft }

        const string PanelClass = "sm-scene-panel";

        readonly string _title;
        VisualElement _card;
        SceneView _host;
        Action _onClose;
        EventCallback<PointerDownEvent> _outsidePress;
        bool _open;

        public SmartMouseScenePanel(string title) { _title = title; }

        public bool IsOpen => _open;
        public bool IsPointerOver { get; private set; }

        public bool Open(VisualElement content, Corner corner, Vector2 margin, float minWidth, Action onClose = null)
        {
            if (_open) return true;

            _host = SceneView.lastActiveSceneView;
            if (_host == null || _host.rootVisualElement == null) return false;

            _open = true;
            _onClose = onClose;
            _card = BuildCard(content, minWidth);
            _host.rootVisualElement.Add(_card);

            _card.style.left = margin.x;
            if (corner == Corner.TopLeft) _card.style.top = margin.y;
            else _card.style.bottom = margin.y;

            _outsidePress = OnOutsidePress;
            _host.rootVisualElement.RegisterCallback(_outsidePress, TrickleDown.TrickleDown);
            _host.Repaint();
            return true;
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            IsPointerOver = false;

            if (_host != null && _host.rootVisualElement != null && _outsidePress != null)
                _host.rootVisualElement.UnregisterCallback(_outsidePress, TrickleDown.TrickleDown);
            _outsidePress = null;

            _card?.RemoveFromHierarchy();
            _card = null;

            SceneView host = _host;
            _host = null;
            Action onClose = _onClose;
            _onClose = null;
            onClose?.Invoke();
            if (host != null) host.Repaint();
        }

        VisualElement BuildCard(VisualElement content, float minWidth)
        {
            var card = new VisualElement();
            card.AddToClassList(PanelClass);
            SmartMouseStyle.Apply(card);
            card.style.position = Position.Absolute;
            card.style.minWidth = minWidth;
            card.pickingMode = PickingMode.Position;
            card.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
            card.RegisterCallback<PointerEnterEvent>(_ => IsPointerOver = true);
            card.RegisterCallback<PointerLeaveEvent>(_ => IsPointerOver = false);
            card.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                // A Scene View can close without an outside click. Defer so an intentional Close(),
                // which clears _open before detaching, remains a no-op here.
                if (_open)
                    EditorApplication.delayCall += CloseIfDetached;
            });

            var titleBar = SmartMouseUI.TitleBar(_title);
            titleBar.AddManipulator(new SmartMousePanelDragger(card));
            card.Add(titleBar);

            var body = new VisualElement();
            body.AddToClassList("sm-scene-body");
            body.Add(content);
            card.Add(body);
            return card;
        }

        void CloseIfDetached()
        {
            if (_open && (_card == null || _card.panel == null))
                Close();
        }

        void OnOutsidePress(PointerDownEvent evt)
        {
            if (!_open || _card == null) return;
            if (!_card.worldBound.Contains((Vector2)evt.position)) Close();
        }

        // Removes any card left parented on a Scene View after a domain reload (statics reset without Close).
        [InitializeOnLoadMethod]
        static void CleanupOrphans()
        {
            foreach (SceneView sv in Resources.FindObjectsOfTypeAll<SceneView>())
            {
                if (sv.rootVisualElement == null) continue;
                foreach (VisualElement stray in sv.rootVisualElement.Query(className: PanelClass).ToList())
                    stray.RemoveFromHierarchy();
            }
        }
    }
}
