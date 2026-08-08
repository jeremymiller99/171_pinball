/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using System;

namespace TelePresent.SmartMouse
{
    // Adds entries to the Smart Mouse context menu:

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class SmartMouseContextMenuItemAttribute : Attribute
    {
        // Lower values place items earlier; the default lists after the built-in groups.
        public int Order { get; }

        // Menu path for the simple form ("Category/Item"); null means the method is a full registration method.
        public string Path { get; }

        // Simple form only: grey the item out while nothing is selected.
        public bool RequiresSelection { get; set; }

        public SmartMouseContextMenuItemAttribute(int order = 1000)
        {
            Order = order;
        }

        public SmartMouseContextMenuItemAttribute(string path, int order = 1000)
        {
            Path = path;
            Order = order;
        }
        
        public SmartMouseContextMenuItemAttribute(string path, bool requiresSelection, int order = 1000)
        {
            Path = path;
            RequiresSelection = requiresSelection;
            Order = order;
        }
    }
}
