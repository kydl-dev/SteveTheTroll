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
        string _owner;

        public bool Complete { get { return _slipLeft != null && _slipRight != null && _trollLeft != null && _trollRight != null; } }

        /// Expected outcome: all four parts exist (returns the set; check Complete for partial failures, logged).
        public static SlipperSet Spawn(Transform character, string owner)
        {
            var set = new SlipperSet { _owner = owner };
            var leftFoot = character != null ? Utils.FindChild(character, "LeftFoot") : null;
            var rightFoot = character != null ? Utils.FindChild(character, "RightFoot") : null;
            ModLog.Verbose("SlipperSet: spawn on " + owner + ", feet " + (leftFoot != null) + " / " + (rightFoot != null));
            set._slipLeft = SpawnSlipper(character, leftFoot, true, owner);
            set._slipRight = SpawnSlipper(character, rightFoot, false, owner);
            set._trollLeft = SpawnTroll(character, leftFoot, true, owner, Driver(set._slipLeft));
            set._trollRight = SpawnTroll(character, rightFoot, false, owner, Driver(set._slipRight));
            ModLog.Verbose("SlipperSet: " + owner + " slippers " + (set._slipLeft != null) + "/" + (set._slipRight != null) + ", trolls " + (set._trollLeft != null) + "/" + (set._trollRight != null));
            ModLog.Check("SlipperSet: " + owner + " parts built", (set._slipLeft != null ? 1 : 0) + (set._slipRight != null ? 1 : 0) + (set._trollLeft != null ? 1 : 0) + (set._trollRight != null ? 1 : 0), 4, 0, "");
            return set;
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
