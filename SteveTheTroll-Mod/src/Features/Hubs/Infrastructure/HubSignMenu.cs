using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using KydlCore.Common;
using KydlCore.Features.Hub.Contracts;
using SteveTheTroll.Bootstrap;
using SteveTheTroll.Ui;
using UnityEngine;

namespace SteveTheTroll.Features.Hubs.Infrastructure;

/// <summary>
///     D-135: the Troll Hub sign editor (SignMenuView of the Unity project) in a KydlCore window. Opened with E on the
///     sign or with the pen in the
///     Troll Hub tab (F6). Escape / X / Cancel close it without saving. While it is open the sign shows the not-yet-saved
///     look.
/// </summary>
internal sealed class HubSignMenu : ISignMenuHost
{
    private const string WindowId = "hubsign", LockName = "HubSignMenu";
    private static HubSignMenu _open;

    private readonly HubSign
        _sign; // null while the hub is not loaded (F6 pen from far away): no preview on the sign then

    private readonly ZDO _zdo;
    private SignMenuView _view;
    private KyWindow _window;

    private HubSignMenu(HubSign sign, ZDO zdo)
    {
        _sign = sign;
        _zdo = zdo;
    }

    internal static bool IsOpen => _open != null;

    private static string PrefPath => Path.Combine(Paths.ConfigPath, "SteveTheTroll.HubSignStyle.txt");

    public string T(string key, params string[] args)
    {
        return Loc.T(key, args);
    }

    public IList<string> Fonts => SignFonts.All();
    public int MaxLetters => HubSign.MaxName;

    public void Preview(string name, SignStyle style)
    {
        if (_sign != null)
        {
            _sign.Preview(name, style);
        }
    }

    public void Save(string name, SignStyle style)
    {
        if (_sign != null)
        {
            _sign.Save(name, style);
        }
        else if (_zdo != null)
        {
            _zdo.SetOwner(ZDOMan.GetSessionID());
            _zdo.Set(HubRegistry.NameKey, HubSign.Clean(name));
            _zdo.Set(HubRegistry.StyleKey, style.Serialize());
            ModLog.Info("HubSignMenu: saved '" + HubSign.Clean(name) + "' on a hub that is not loaded");
            HubRegistry.All(true);
        }

        Close("saved");
    }

    public void Close(string why)
    {
        if (_window != null)
        {
            _window.Close();
        }
        else
        {
            Closed();
        }

        _window = null;
        ModLog.Verbose("HubSignMenu: closed (" + why + ")");
    }

    public int SavePreferred(SignStyle style)
    {
        try
        {
            File.WriteAllText(PrefPath, style.Serialize());
            return style.Serialize().Length;
        }
        catch (Exception e)
        {
            ModLog.Warn("HubSignMenu: could not save the preferred style: " + e.Message);
            return 0;
        }
    }

    public SignStyle LoadPreferred()
    {
        try
        {
            return File.Exists(PrefPath) ? SignStyle.Parse(File.ReadAllText(PrefPath).Trim()) : null;
        }
        catch (Exception e)
        {
            ModLog.Warn("HubSignMenu: could not read the preferred style: " + e.Message);
            return null;
        }
    }

    /// Opens the editor for `sign`. Expected outcome: 1 when open, 0 when the UI is not available.
    internal static int Open(HubSign sign)
    {
        if (sign == null)
        {
            return 0;
        }

        return Show(new HubSignMenu(sign, null), sign.SignName, sign.Style);
    }

    /// The F6 pen: works from anywhere (the sign is used for the preview when its hub is loaded). Expected outcome: 1 when open.
    internal static int Open(HubInfo hub)
    {
        if (hub == null || hub.Zdo == null)
        {
            return 0;
        }

        var sign = HubSign.Of(hub.Owner, hub.Number);
        var style = SignStyle.Parse(hub.Zdo.GetString(HubRegistry.StyleKey));
        return Show(new HubSignMenu(sign, hub.Zdo), hub.Name, style);
    }

    private static int Show(HubSignMenu m, string name, SignStyle style)
    {
        if (_open != null)
        {
            _open.Close("another sign opened");
        }

        m._view = new SignMenuView(m, name, style);
        m._window = KyUi.Open(Plugin.ModGuid, WindowId, m._view.Window, () => m.Closed());
        if (m._window == null)
        {
            ModLog.Warn("HubSignMenu: the window could not be opened");
            return 0;
        }

        _open = m;
        UiInputLock.Hold(LockName);
        return 1;
    }

    /// Plugin.Update: Escape closes, so does the sign being gone.
    internal static void Tick()
    {
        HubSignMenu m = _open;
        if (m == null)
        {
            return;
        }

        if (m._sign == null && m._zdo == null)
        {
            m.Close("the hub is gone");
        }
        else if (ZInput.GetKeyDown(KeyCode.Escape))
        {
            m.Close("Escape");
        }
    }

    private void Closed()
    {
        if (_sign != null)
        {
            _sign.EndPreview();
        }

        if (_open == this)
        {
            _open = null;
            UiInputLock.Release(LockName);
        }
    }
}
