using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Trolls.Registry.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Ui
{
    /// <summary>
    /// D-081 (user: "add icons, of the head piece item, for showing each troll on the map (its last known location)"): one map pin per living
    /// own tamed troll from TrollRegistry (live position while loaded, else where it was last seen), with the costume head icon and the troll's
    /// name. Pins are not saved with the map (made again every session) and are removed when a troll dies or leaves the list.
    /// </summary>
    internal static class TrollMapPins
    {
        const string IconName = BundleAssets.CostumeHelmetIcon;
        const float RefreshSeconds = 2f;
        static readonly Dictionary<string, Minimap.PinData> Pins = new Dictionary<string, Minimap.PinData>();
        static readonly System.Reflection.MethodInfo AddPinMethod = AccessTools.Method(typeof(Minimap), "AddPin");
        static Minimap _map;
        static Sprite _icon;
        static bool _iconLooked;
        static float _next;

        /// Plugin.Update. Expected outcome: how many pins exist after this refresh (-1 = nothing done this frame).
        internal static int Tick()
        {
            if (Time.time < _next) return -1;
            _next = Time.time + RefreshSeconds;
            var map = Minimap.instance;
            if (map != _map) { Pins.Clear(); _map = map; }                        // a new world / map: the old pins went with the old map
            if (map == null || Player.m_localPlayer == null || !ModSettings.On || !ModSettings.TrollMapPins.Value) { Clear(); return 0; }
            var icon = Icon();
            var alive = new HashSet<string>();
            foreach (var t in TrollRegistry.Peek())
            {
                if (t.DiedTicks > 0) continue;
                alive.Add(t.Id);
                var pos = new Vector3(t.X, t.Y, t.Z);
                string label = t.Shown(k => Loc.T(k));
                Minimap.PinData pin;
                if (!Pins.TryGetValue(t.Id, out pin) || pin == null)
                {
                    pin = Add(map, pos, label);
                    if (pin == null) continue;
                    if (icon != null) pin.m_icon = icon;
                    Pins[t.Id] = pin;
                    ModLog.Verbose("TrollMapPins: pin for " + label + " [" + t.Id + "] at " + pos.ToString("F0") + (t.Loaded ? " (live)" : " (last seen)") + ", icon " + (icon != null));
                    continue;
                }
                pin.m_pos = pos;
                if (pin.m_name != label) pin.m_name = label;
            }
            var gone = new List<string>();
            foreach (var kv in Pins) if (!alive.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var id in gone) { map.RemovePin(Pins[id]); Pins.Remove(id); ModLog.Verbose("TrollMapPins: pin of [" + id + "] removed (dead / gone)"); }
            return Pins.Count;
        }

        static void Clear()
        {
            if (_map != null) foreach (var p in Pins.Values) if (p != null) _map.RemovePin(p);
            Pins.Clear();
        }

        static Sprite Icon()
        {
            if (_iconLooked) return _icon;
            _iconLooked = true;
            _icon = BundleAssets.Load<Sprite>(IconName);
            if (_icon == null) ModLog.Warn("TrollMapPins: icon '" + IconName + "' not in the bundle - the game's pin icon is used");
            return _icon;
        }

        // Minimap.AddPin(pos, type, name, save, isChecked, ownerID, author): the last parameter's type lives in an assembly the mod does not
        // reference, so the call goes through reflection with that parameter's default value.
        static Minimap.PinData Add(Minimap map, Vector3 pos, string name)
        {
            if (AddPinMethod == null) { ModLog.Warn("TrollMapPins: Minimap.AddPin not found"); return null; }
            var ps = AddPinMethod.GetParameters();
            var args = new object[ps.Length];
            args[0] = pos; args[1] = Minimap.PinType.Icon3; args[2] = name; args[3] = false; args[4] = false;
            for (int i = 5; i < ps.Length; i++) args[i] = ps[i].ParameterType == typeof(long) ? (object)0L : (ps[i].ParameterType.IsValueType ? System.Activator.CreateInstance(ps[i].ParameterType) : null);
            return AddPinMethod.Invoke(map, args) as Minimap.PinData;
        }
    }
}
