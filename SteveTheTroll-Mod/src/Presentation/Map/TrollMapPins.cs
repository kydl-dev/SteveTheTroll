using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;
using SteveTheTroll.Integrations.Unity;
using SteveTheTroll.Ui;
using UnityEngine;

namespace SteveTheTroll.Presentation.Map;

/// <summary>
///     D-081 (user: "add icons, of the head piece item, for showing each troll on the map (its last known location)"): one
///     map pin per living
///     own tamed troll from TrollRegistry (live position while loaded, else where it was last seen), with the costume head
///     icon and the troll's
///     name. Pins are not saved with the map (made again every session) and are removed when a troll dies or leaves the
///     list.
/// </summary>
internal static class TrollMapPins
{
    private const string IconName = BundleAssets.CostumeHelmetIcon;
    private const float RefreshSeconds = 2f;
    private static readonly Dictionary<string, Minimap.PinData> Pins = new();
    private static readonly MethodInfo AddPinMethod = AccessTools.Method(typeof(Minimap), "AddPin");
    private static Minimap _map;
    private static Sprite _icon;
    private static bool _iconLooked;
    private static float _next;

    /// Plugin.Update. Expected outcome: how many pins exist after this refresh (-1 = nothing done this frame).
    internal static int Tick()
    {
        if (Time.time < _next)
        {
            return -1;
        }

        _next = Time.time + RefreshSeconds;
        Minimap map = Minimap.instance;
        if (map != _map)
        {
            Pins.Clear();
            _map = map;
        } // a new world / map: the old pins went with the old map

        if (map == null || Player.m_localPlayer == null || !ModSettings.On || !ModSettings.TrollMapPins.Value)
        {
            Clear();
            return 0;
        }

        Sprite icon = Icon();
        var alive = new HashSet<string>();
        foreach (TrollInfo t in TrollRegistry.Peek())
        {
            if (t.DiedTicks > 0)
            {
                continue;
            }

            alive.Add(t.Id);
            var pos = new Vector3(t.X, t.Y, t.Z);
            string label = t.Shown(k => Loc.T(k));
            Minimap.PinData pin;
            if (!Pins.TryGetValue(t.Id, out pin) || pin == null)
            {
                pin = Add(map, pos, label);
                if (pin == null)
                {
                    continue;
                }

                if (icon != null)
                {
                    pin.m_icon = icon;
                }

                Pins[t.Id] = pin;
                ModLog.Verbose("TrollMapPins: pin for " + label + " [" + t.Id + "] at " + pos.ToString("F0") +
                               (t.Loaded ? " (live)" : " (last seen)") + ", icon " + (icon != null));
                continue;
            }

            pin.m_pos = pos;
            if (pin.m_name != label)
            {
                pin.m_name = label;
            }
        }

        var gone = new List<string>();
        foreach (KeyValuePair<string, Minimap.PinData> kv in Pins)
        {
            if (!alive.Contains(kv.Key))
            {
                gone.Add(kv.Key);
            }
        }

        foreach (string id in gone)
        {
            map.RemovePin(Pins[id]);
            Pins.Remove(id);
            ModLog.Verbose("TrollMapPins: pin of [" + id + "] removed (dead / gone)");
        }

        return Pins.Count;
    }

    private static void Clear()
    {
        if (_map != null)
        {
            foreach (Minimap.PinData p in Pins.Values)
            {
                if (p != null)
                {
                    _map.RemovePin(p);
                }
            }
        }

        Pins.Clear();
    }

    private static Sprite Icon()
    {
        if (_iconLooked)
        {
            return _icon;
        }

        _iconLooked = true;
        _icon = BundleAssets.Load<Sprite>(IconName);
        if (_icon == null)
        {
            ModLog.Warn("TrollMapPins: icon '" + IconName + "' not in the bundle - the game's pin icon is used");
        }

        return _icon;
    }

    // Minimap.AddPin(pos, type, name, save, isChecked, ownerID, author): the last parameter's type lives in an assembly the mod does not
    // reference, so the call goes through reflection with that parameter's default value.
    private static Minimap.PinData Add(Minimap map, Vector3 pos, string name)
    {
        if (AddPinMethod == null)
        {
            ModLog.Warn("TrollMapPins: Minimap.AddPin not found");
            return null;
        }

        ParameterInfo[] ps = AddPinMethod.GetParameters();
        object[] args = new object[ps.Length];
        args[0] = pos;
        args[1] = Minimap.PinType.Icon3;
        args[2] = name;
        args[3] = false;
        args[4] = false;
        for (int i = 5; i < ps.Length; i++)
        {
            args[i] = ps[i].ParameterType == typeof(long) ? 0L :
                ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null;
        }

        return AddPinMethod.Invoke(map, args) as Minimap.PinData;
    }
}
