/*******************************************************
Product - Smart Mouse Selection Tools
Publisher - TelePresent Games
			http://TelePresentGames.dk
Author    - Martin Hansen
Created   - 2026
(c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using UnityEditor;
using UnityEngine;
namespace TelePresent.SmartMouse
{
	internal static class SmartMouseLabelRenderer
	{
		static GUIStyle _headerStyle;
		static GUIStyle _valueStyle;

		static readonly Color BackgroundColor = new Color(0f, 0f, 0f, 0.5f);
		const float BackgroundCornerRadius = 8f;

		public static void DrawWorldPositionLabel(Vector2 mousePosition, Vector3 worldPoint, SceneView sceneView, GameObject hitObject = null)
		{
			string header = SmartMouseContextMenu.IsFocusOnGameObjects() && hitObject != null ? hitObject.name : string.Empty;
			string values = FormatPosition(worldPoint);

			if (!string.IsNullOrEmpty(header) || !string.IsNullOrEmpty(values))
			{
				header = string.IsNullOrEmpty(header) ? "World Position:" : header;
				DrawLabel(mousePosition, header, values, sceneView);
			}
		}

		static string FormatPosition(Vector3 p)
		{
			const string punct = "#8C939E";
			return $"<color={punct}>(</color><color=#F4675B>{p.x:F2}</color><color={punct}>, </color><color=#8FD157>{p.y:F2}</color><color={punct}>, </color><color=#5B8DEF>{p.z:F2}</color><color={punct}>)</color>";
		}

		static void EnsureStyles()
		{
			if (_headerStyle == null)
			{
				_headerStyle = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleCenter,
					fontSize = 13,
					fontStyle = FontStyle.Bold,
					normal =
					{
						textColor = Color.white
					}
				};
			}
			if (_valueStyle == null)
			{
				_valueStyle = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleCenter,
					fontSize = 11,
					richText = true,
					normal =
					{
						textColor = new Color(0.85f, 0.88f, 0.92f)
					}
				};
			}
		}

		static void DrawLabel(Vector2 mousePosition, string header, string values, SceneView sceneView)
		{
			GUI.depth = -1;

			EnsureStyles();

			Vector2 headerSize = _headerStyle.CalcSize(new GUIContent(header));
			Vector2 valuesSize = string.IsNullOrEmpty(values) ? Vector2.zero : _valueStyle.CalcSize(new GUIContent(values));
			float horizontalMargin = 12f;
			float verticalPadding = 5f;
			float lineGap = string.IsNullOrEmpty(values) ? 0f : 2f;
			float totalWidth = Mathf.Max(headerSize.x, valuesSize.x) + 2 * horizontalMargin;
			float totalHeight = headerSize.y + valuesSize.y + lineGap + 2 * verticalPadding;
			Vector2 labelPosition = mousePosition + new Vector2(-totalWidth / 2, 20);

			labelPosition.x = Mathf.Clamp(labelPosition.x, 10f, sceneView.position.width - totalWidth - 10f);
			labelPosition.y = Mathf.Clamp(labelPosition.y, 10f, sceneView.position.height - totalHeight - 10f);

			Handles.BeginGUI();
			DrawBackgroundBox(labelPosition, totalWidth, totalHeight);
			DrawLabelContent(labelPosition, totalWidth, header, values, headerSize, valuesSize, horizontalMargin, verticalPadding, lineGap);
			Handles.EndGUI();
		}

		static void DrawBackgroundBox(Vector2 labelPosition, float width, float height)
		{
			GUI.DrawTexture(new Rect(labelPosition.x, labelPosition.y, width, height),
				Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
				BackgroundColor, 0f, BackgroundCornerRadius);
		}

		static void DrawLabelContent(Vector2 labelPosition, float totalWidth, string header, string values, Vector2 headerSize, Vector2 valuesSize, float horizontalMargin, float verticalPadding, float lineGap)
		{
			float contentWidth = totalWidth - 2 * horizontalMargin;
			GUI.Label(new Rect(labelPosition.x + horizontalMargin, labelPosition.y + verticalPadding, contentWidth, headerSize.y), header, _headerStyle);
			if (!string.IsNullOrEmpty(values))
				GUI.Label(new Rect(labelPosition.x + horizontalMargin, labelPosition.y + verticalPadding + headerSize.y + lineGap, contentWidth, valuesSize.y), values, _valueStyle);
		}

	}
}
