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
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace TelePresent.SmartMouse
{

    internal class WelcomeLink
    {
        public string Title;
        public string Description;
        public Texture2D Icon;
        public Action OnClick;
        public bool Pulse;

        public static WelcomeLink Url(string title, string description, string url, Texture2D icon = null, bool pulse = false)
        {
            return new WelcomeLink
            {
                Title = title,
                Description = description,
                Icon = icon,
                Pulse = pulse,
                OnClick = () => Application.OpenURL(url)
            };
        }
    }

    internal class WelcomeAction
    {
        public string Label;
        public string DoneLabel;
        public Func<bool> IsDone;
        public Action Invoke;
        
        public Action<Action> Subscribe;
        public Action<Action> Unsubscribe;
    }

    internal class WelcomeWindowConfig
    {
        public WelcomeAction PrimaryAction;
        public string Title = "Welcome!";
        public string Intro = "";
        public List<WelcomeLink> Links = new List<WelcomeLink>();
        public string NewsUrl;
        public string NewsTitle = "News";
        public string Footer = "";
        public Color Accent = new Color(0.231f, 0.471f, 0.871f);
        public Func<bool> GetShowOnStartup;
        public Action<bool> SetShowOnStartup;
        public Vector2 MinSize = new Vector2(480, 640);
        public string StyleSheetName = "WelcomeWindow";
        public string StyleSheetGuid;
        // Per product, like the sheet above; empty falls back to "<StyleSheetName>_Motion" by name.
        public string MotionStyleSheetGuid;
    }

    internal class NewsItem
    {
        public string Date;
        public string Headline;
        public string Body;
    }

    internal abstract class WelcomeWindowBase : EditorWindow
    {
        WelcomeWindowConfig _config;
        VisualElement _newsList;

        protected abstract WelcomeWindowConfig BuildConfig();

        void CreateGUI()
        {
            _config = BuildConfig();
            minSize = _config.MinSize;

            VisualElement root = rootVisualElement;
            ApplyStyleSheet(root, _config.StyleSheetName, _config.StyleSheetGuid,
                _config.MotionStyleSheetGuid);
            root.AddToClassList("ww-window");

            var scroll = new ScrollView(ScrollViewMode.Vertical);
#if UNITY_2022_1_OR_NEWER
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
#else
            scroll.showHorizontal = false;
#endif
            scroll.AddToClassList("ww-scroll");
            root.Add(scroll);

            var content = new VisualElement();
            content.AddToClassList("ww-content");
            scroll.Add(content);

            content.Add(BuildHeader());

            if (_config.PrimaryAction != null)
                content.Add(BuildPrimaryAction());

            content.Add(BuildLinks());

            if (_config.GetShowOnStartup != null && _config.SetShowOnStartup != null)
                content.Add(BuildStartupToggle());

            if (!string.IsNullOrEmpty(_config.NewsUrl))
            {
                content.Add(BuildNews());
                FetchNews(_config.NewsUrl);
            }

            if (!string.IsNullOrEmpty(_config.Footer))
                content.Add(BuildFooter());
        }

        VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("ww-header");

            var title = new Label(_config.Title);
            title.AddToClassList("ww-title");
            header.Add(title);

            if (!string.IsNullOrEmpty(_config.Intro))
            {
                var intro = new Label(_config.Intro);
                intro.AddToClassList("ww-intro");
                header.Add(intro);
            }

            return header;
        }

        VisualElement BuildPrimaryAction()
        {
            WelcomeAction action = _config.PrimaryAction;
            var button = new Button();
            button.AddToClassList("ww-cta");

            void Refresh()
            {
                bool done = action.IsDone != null && action.IsDone();
                button.text = done ? action.DoneLabel : action.Label;
                button.SetEnabled(!done);
                button.EnableInClassList("ww-cta--done", done);
                button.style.backgroundColor = done ? new StyleColor(StyleKeyword.Null) : _config.Accent;
            }

            button.clicked += () => { action.Invoke?.Invoke(); Refresh(); };

            if (action.Subscribe != null && action.Unsubscribe != null)
            {
                button.RegisterCallback<AttachToPanelEvent>(_ => { action.Subscribe(Refresh); Refresh(); });
                button.RegisterCallback<DetachFromPanelEvent>(_ => action.Unsubscribe(Refresh));
            }

            Refresh();
            return button;
        }

        VisualElement BuildLinks()
        {
            var wrap = new VisualElement();
            wrap.AddToClassList("ww-links");

            foreach (WelcomeLink link in _config.Links)
                if (link != null)
                    wrap.Add(BuildLinkCard(link));

            return wrap;
        }

        VisualElement BuildLinkCard(WelcomeLink link)
        {
            var card = new VisualElement();
            card.AddToClassList("ww-card");
            card.tooltip = link.Description;

            var bar = new VisualElement();
            bar.AddToClassList("ww-card-bar");
            bar.style.backgroundColor = _config.Accent;
            card.Add(bar);

            if (link.Icon != null)
            {
                var icon = new Image { image = link.Icon, scaleMode = ScaleMode.ScaleToFit };
                icon.AddToClassList("ww-card-icon");
                card.Add(icon);
            }

            var text = new VisualElement();
            text.AddToClassList("ww-card-text");

            var title = new Label(link.Title);
            title.AddToClassList("ww-card-title");
            text.Add(title);

            if (!string.IsNullOrEmpty(link.Description))
            {
                var desc = new Label(link.Description);
                desc.AddToClassList("ww-card-desc");
                text.Add(desc);
            }

            card.Add(text);
            card.AddManipulator(new Clickable(() => link.OnClick?.Invoke()));

            if (link.Pulse)
            {
                Color baseColor = _config.Accent;
                Color bright = Color.Lerp(baseColor, Color.white, 0.45f);
                bar.schedule.Execute(() =>
                {
                    float t = (Mathf.Sin((float)EditorApplication.timeSinceStartup * 3f) + 1f) * 0.5f;
                    bar.style.backgroundColor = Color.Lerp(baseColor, bright, t);
                }).Every(33);
            }

            return card;
        }

        VisualElement BuildStartupToggle()
        {
            var row = new VisualElement();
            row.AddToClassList("ww-toggle-row");

            var toggle = new Toggle(" Show this window on start-up") { value = _config.GetShowOnStartup() };
            toggle.AddToClassList("ww-toggle");
            toggle.RegisterValueChangedCallback(evt => _config.SetShowOnStartup(evt.newValue));

            row.Add(toggle);
            return row;
        }

        VisualElement BuildNews()
        {
            var section = new VisualElement();
            section.AddToClassList("ww-news");

            var header = new Label(_config.NewsTitle);
            header.AddToClassList("ww-section-title");
            section.Add(header);

            _newsList = new VisualElement();
            _newsList.AddToClassList("ww-news-list");
            section.Add(_newsList);

            SetNewsStatus("Loading news...");
            return section;
        }

        VisualElement BuildFooter()
        {
            var footer = new VisualElement();
            footer.AddToClassList("ww-footer");

            var label = new Label(_config.Footer);
            label.AddToClassList("ww-muted");
            footer.Add(label);
            return footer;
        }

        VisualElement BuildNewsCard(NewsItem item)
        {
            var card = new VisualElement();
            card.AddToClassList("ww-news-card");

            var top = new VisualElement();
            top.AddToClassList("ww-news-top");

            if (!string.IsNullOrEmpty(item.Date))
            {
                var date = PlainTextLabel(item.Date);
                date.AddToClassList("ww-news-date");
                date.style.color = _config.Accent;
                top.Add(date);
            }

            var headline = PlainTextLabel(item.Headline);
            headline.AddToClassList("ww-news-headline");
            top.Add(headline);
            card.Add(top);

            if (!string.IsNullOrEmpty(item.Body))
            {
                var body = PlainTextLabel(item.Body);
                body.AddToClassList("ww-news-body");
                card.Add(body);
            }

            return card;
        }

        static Label PlainTextLabel(string text)
        {
#if UNITY_2022_1_OR_NEWER
            return new Label(text) { enableRichText = false };
#else
            return new Label(text);
#endif
        }

        void SetNewsStatus(string message)
        {
            if (_newsList == null) return;
            _newsList.Clear();

            var label = PlainTextLabel(message);
            label.AddToClassList("ww-status");
            _newsList.Add(label);
        }

        void PopulateNews(List<NewsItem> items)
        {
            if (_newsList == null) return;

            if (items.Count == 0)
            {
                SetNewsStatus("No news right now.");
                return;
            }

            _newsList.Clear();
            foreach (NewsItem item in items)
                _newsList.Add(BuildNewsCard(item));
        }

        async void FetchNews(string url)
        {
            try
            {
                using UnityWebRequest request = UnityWebRequest.Get(url);
                request.timeout = 8;
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();

                while (!operation.isDone)
                    await Task.Yield();

                if (this == null || _newsList == null) return;

                if (request.result != UnityWebRequest.Result.Success)
                    SetNewsStatus("Couldn't load news right now.");
                else
                    PopulateNews(ParseNews(request.downloadHandler.text));
            }
            catch (Exception)
            {
                if (this == null || _newsList == null) return;
                SetNewsStatus("Couldn't load news right now.");
            }
        }

        static List<NewsItem> ParseNews(string rawText)
        {
            var items = new List<NewsItem>();
            string[] separators = { "\n---\n", "\r\n---\r\n" };

            foreach (string entry in rawText.Split(separators, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] lines = entry.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length < 3) continue;

                string date = lines[0].Trim();
                if (date.StartsWith("[") && date.EndsWith("]"))
                    date = date.Substring(1, date.Length - 2);

                items.Add(new NewsItem
                {
                    Date = date,
                    Headline = lines[1].Trim(),
                    Body = string.Join("\n", lines, 2, lines.Length - 2).Trim()
                });
            }

            return items;
        }

        static void ApplyStyleSheet(VisualElement element, string sheetName, string sheetGuid,
            string motionSheetGuid = null)
        {
            if (string.IsNullOrEmpty(sheetName)) return;

            StyleSheet sheet = null;
            if (!string.IsNullOrEmpty(sheetGuid))
            {
                string path = AssetDatabase.GUIDToAssetPath(sheetGuid);
                if (!string.IsNullOrEmpty(path))
                    sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            }

            if (sheet == null)
            {
                foreach (string guid in AssetDatabase.FindAssets($"{sheetName} t:StyleSheet"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.Equals(System.IO.Path.GetFileNameWithoutExtension(path), sheetName,
                            StringComparison.OrdinalIgnoreCase))
                        continue;
                    sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                    if (sheet != null) break;
                }
            }
            if (sheet != null && !element.styleSheets.Contains(sheet))
                element.styleSheets.Add(sheet);

#if UNITY_2022_1_OR_NEWER

            StyleSheet motionByGuid = null;
            if (!string.IsNullOrEmpty(motionSheetGuid))
            {
                string byGuid = AssetDatabase.GUIDToAssetPath(motionSheetGuid);
                if (!string.IsNullOrEmpty(byGuid))
                    motionByGuid = AssetDatabase.LoadAssetAtPath<StyleSheet>(byGuid);
            }
            if (motionByGuid != null)
            {
                if (!element.styleSheets.Contains(motionByGuid)) element.styleSheets.Add(motionByGuid);
                return;
            }

            foreach (string guid in AssetDatabase.FindAssets(sheetName + "_Motion t:StyleSheet"))
            {
                string motionPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(System.IO.Path.GetFileNameWithoutExtension(motionPath), sheetName + "_Motion",
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                StyleSheet motion = AssetDatabase.LoadAssetAtPath<StyleSheet>(motionPath);
                if (motion != null && !element.styleSheets.Contains(motion))
                    element.styleSheets.Add(motion);
                break;
            }
#endif
        }
    }
}
