using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Figure
{
    /// <summary>
    /// The full look on one character: a plush slipper on each foot (SlipperDriver, skinned to the character's own foot + toe bones) and a
    /// mini troll on each (TrollFigure). Used by the worn item (SlipperWearer, every player) and the dev command stt_preview.
    /// Feet are looked up BEFORE anything is parented to the character: the troll copy has bones with the same names.
    /// </summary>
    internal sealed class SlipperSet
    {
        GameObject _slipLeft, _slipRight, _trollLeft, _trollRight;
        Transform _character;
        string _owner;

        public bool Complete { get { return _slipLeft != null && _slipRight != null && _trollLeft != null && _trollRight != null; } }

        /// The troll copies come from ZNetScene: a player loading in with the slippers on is dressed before it exists (TEST-008).
        public static bool WorldReady { get { return ZNetScene.instance != null; } }

        /// Expected outcome: all four parts exist, or the slippers now and the trolls once the world is ready (Fill again; SlipperWearer does).
        public static SlipperSet Spawn(Transform character, string owner)
        {
            var set = new SlipperSet { _owner = owner, _character = character };
            set.Fill();
            return set;
        }

        /// Spawns whatever part is missing. Expected outcome: returns how many parts exist now (4 = complete).
        public int Fill()
        {
            var leftFoot = _character != null ? Utils.FindChild(_character, "LeftFoot") : null;
            var rightFoot = _character != null ? Utils.FindChild(_character, "RightFoot") : null;
            ModLog.Verbose("SlipperSet: fill on " + _owner + ", feet " + (leftFoot != null) + " / " + (rightFoot != null) + ", world ready " + WorldReady);
            if (_slipLeft == null) _slipLeft = SpawnSlipper(_character, leftFoot, true, _owner);
            if (_slipRight == null) _slipRight = SpawnSlipper(_character, rightFoot, false, _owner);
            if (WorldReady && _trollLeft == null) _trollLeft = SpawnTroll(_character, leftFoot, true, _owner, Driver(_slipLeft));
            if (WorldReady && _trollRight == null) _trollRight = SpawnTroll(_character, rightFoot, false, _owner, Driver(_slipRight));
            int n = (_slipLeft != null ? 1 : 0) + (_slipRight != null ? 1 : 0) + (_trollLeft != null ? 1 : 0) + (_trollRight != null ? 1 : 0);
            ModLog.Verbose("SlipperSet: " + _owner + " slippers " + (_slipLeft != null) + "/" + (_slipRight != null) + ", trolls " + (_trollLeft != null) + "/" + (_trollRight != null));
            if (WorldReady) ModLog.Check("SlipperSet: " + _owner + " parts built", n, 4, 0, "");
            else ModLog.Verbose("SlipperSet: " + _owner + " trolls wait for the world (ZNetScene not ready yet)");
            return n;
        }

        /// Expected outcome: nothing of the set is left (returns that check).
        public bool Despawn()
        {
            foreach (var go in new[] { _slipLeft, _slipRight, _trollLeft, _trollRight }) if (go != null) Object.Destroy(go);
            _slipLeft = _slipRight = _trollLeft = _trollRight = null;
            ModLog.Verbose("SlipperSet: " + _owner + " removed");
            return !Complete;
        }

        static GameObject SpawnSlipper(Transform character, Transform foot, bool left, string owner)
        {
            if (foot == null) { ModLog.Warn("SlipperSet: " + owner + " has no " + (left ? "LeftFoot" : "RightFoot") + " bone - no slipper"); return null; }
            var go = new GameObject(left ? "SteveTheTroll_SlipperLeft" : "SteveTheTroll_SlipperRight");
            go.transform.SetParent(character, false);
            bool ok = go.AddComponent<SlipperDriver>().Init(foot, character, left);
            ModLog.Verbose("SlipperSet: " + owner + " " + go.name + " " + (ok ? "built" : "NOT built (see warnings above)"));
            return go;
        }

        static SlipperDriver Driver(GameObject slipper) { return slipper != null ? slipper.GetComponent<SlipperDriver>() : null; }

        /// The troll is glued to its slipper (TEST-007); without one it stands on the foot.
        static GameObject SpawnTroll(Transform character, Transform foot, bool left, string owner, SlipperDriver slipper)
        {
            if (foot == null) return null;
            var holder = TrollFigureFactory.Build(left ? "SteveTheTroll_FigureLeft" : "SteveTheTroll_FigureRight");
            if (holder == null) return null;
            holder.transform.SetParent(character, false);
            bool ready = holder.AddComponent<TrollFigure>().Init(foot, character, left, slipper);
            holder.SetActive(true);
            ModLog.Verbose("SlipperSet: " + owner + " " + holder.name + " active, init " + (ready ? "complete" : "INCOMPLETE (see warnings above)"));
            return holder;
        }
    }
}
