/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace TelePresent.SmartMouse
{

    // Shared UI Toolkit element builders for the Scene-View popups
    internal static class SmartMouseUI
    {
        public static Label Title(string text)
        {
            var label = new Label(text);
            label.AddToClassList("sm-title");
            return label;
        }
        
        public static VisualElement TitleBar(string text)
        {
            var bar = new VisualElement();
            bar.AddToClassList("sm-titlebar");

            bar.Add(Title(text));

            var grip = new VisualElement();
            grip.AddToClassList("sm-grip");
            for (int i = 0; i < 6; i++)
            {
                var dot = new VisualElement();
                dot.AddToClassList("sm-grip-dot");
                grip.Add(dot);
            }
            bar.Add(grip);
            return bar;
        }

        public static Label Help(string text)
        {
            var label = new Label(text);
            label.AddToClassList("sm-help");
            return label;
        }

        public static VisualElement Section()
        {
            var section = new VisualElement();
            section.AddToClassList("sm-section");
            return section;
        }

        public static Button AccentButton(string label, Action onClick)
        {
            var button = new Button(onClick) { text = label };
            button.AddToClassList("sm-accent-button");
            return button;
        }

        // A label + horizontal slider with an inline value field.
        public static Slider SliderRow(string label, string tooltip, float min, float max,
            Func<float> get, Action<float> set, Action onChanged = null)
        {
            var slider = new Slider(label, min, max) { value = get(), showInputField = true };
            slider.tooltip = tooltip;
            slider.RegisterValueChangedCallback(evt =>
            {
                set(evt.newValue);
                onChanged?.Invoke();
            });
            return slider;
        }

        // The pill toggle from the toolbar/Surface Snap panel
        public static VisualElement SwitchRow(string label, string tooltip, Func<bool> get, Action<bool> set,
            Action onChanged = null, List<Action> sync = null)
        {
            var row = new VisualElement();
            row.AddToClassList("sm-row");
            row.tooltip = tooltip;

            var caption = new Label(label);
            caption.AddToClassList("sm-row-label");

            var track = new VisualElement();
            track.AddToClassList("sm-switch");
            var knob = new VisualElement();
            knob.AddToClassList("sm-switch-knob");
            track.Add(knob);

            track.EnableInClassList("sm-switch--on", get());

            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                bool next = !get();
                set(next);
                track.EnableInClassList("sm-switch--on", next);
                onChanged?.Invoke();
                evt.StopPropagation();
            });

            sync?.Add(() => track.EnableInClassList("sm-switch--on", get()));

            row.Add(caption);
            row.Add(track);
            return row;
        }
    }
}
