using Kydl.Common;
using Kydl.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteveTheTroll.Ui
{
    /// <summary>
    /// The mod's menu in game ([General] MenuKey): opens / closes with the key, Escape closes; the character does not move while it is open.
    /// Only the GAME side lives here (keys, input lock, game data). What the menu shows is built by MainMenuView (Unity project,
    /// SteveTheTroll-Unity/Assets/SteveTheTroll_UI/Runtime), which the Unity preview shows too - change the look there.
    /// </summary>
    internal static class MainMenu
    {
        const string LockName = "SteveTheTroll.MainMenu";
        static VisualElement _window;

        internal static bool IsOpen { get { return _window != null; } }

        /// Every frame (Plugin.Update). Expected outcome: returns whether the menu is open after this frame.
        internal static bool Tick()
        {
            var key = ModSettings.MenuKey != null ? ModSettings.MenuKey.Value : KeyCode.None;
            if (!IsOpen)
            {
                if (key == KeyCode.None || !ZInput.GetKeyDown(key, true)) return IsOpen;
                if (Chat.instance != null && Chat.instance.HasFocus() || global::Console.IsVisible() || Menu.IsVisible())
                {
                    ModLog.Verbose("MainMenu: key pressed while chat / console / game menu is open - not opened");
                    return IsOpen;
                }
                Open();
                return IsOpen;
            }
            if (ZInput.GetKeyDown(key, true) || ZInput.GetKeyDown(KeyCode.Escape, true)) Close("key");
            return IsOpen;
        }

        static void Open()
        {
            var root = UiKit.Root;
            if (root == null) { ModLog.Warn("MainMenu: UI Toolkit is not available, the menu cannot open"); return; }
            var viewer = new ViewerSettings();
            var data = SlipperMenuData.Read();
            var trolls = Taming.TrollRegistry.Trolls();
            var costume = CostumeMenuData.Read();
            _window = MainMenuView.Build(k => Loc.T(k), () => Close("X"), data, data != null ? () => ViewerScene.Make(SlipperMenuData.MakeModel, viewer) : (System.Func<IModelScene>)null,
                viewer, trolls, (id, v) => SlipperMenuData.FocusChosen(id, v), id => SlipperMenuData.GoFarm(id), 0, System.DateTime.UtcNow.Ticks,
                costume, costume != null ? () => ViewerScene.Make(CostumeMenuData.MakeModel, viewer, 1) : (System.Func<IModelScene>)null);
            root.Add(_window);
            UiInputLock.Hold(LockName);
            ModLog.Verbose("MainMenu: opened (slippers data " + (data != null) + ", " + trolls.Count + " troll(s))");
        }

        static void Close(string why)
        {
            if (_window != null) _window.RemoveFromHierarchy();
            _window = null;
            UiInputLock.Release(LockName);
            ModLog.Verbose("MainMenu: closed (" + why + ")");
        }
    }
}
