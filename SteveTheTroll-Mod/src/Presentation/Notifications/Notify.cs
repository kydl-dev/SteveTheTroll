using KydlCore.Common;
using TMPro;
using UnityEngine;

namespace SteveTheTroll.Presentation.Notifications;

/// <summary>
///     D-047: every message of the mod goes to the LEFT side of the screen (MessageHud TopLeft, normal text), coloured by
///     kind.
///     D-048 (user colours): Farm dark yellow, Attack red, Good green (coming, follow / stay), Death blue; Info white,
///     Warn orange.
/// </summary>
internal static class Notify
{
    private static TMP_Text _timed;
    private static float _timedUntil;

    private static string Colour(Kind k)
    {
        switch (k)
        {
            case Kind.Good: return "#9BE07A";
            case Kind.Warn: return "#FFB347";
            case Kind.Bad: return "#FF6B6B";
            case Kind.Farm: return "#C9A227";
            case Kind.Attack: return "#FF4040";
            case Kind.Death: return "#5AA9FF";
            default: return "#E8E0D0";
        }
    }

    /// The text with the kind's colour (rich text, MessageHud shows it).
    internal static string Paint(string text, Kind kind)
    {
        return "<color=" + Colour(kind) + ">" + text + "</color>";
    }

    /// Expected outcome: returns whether a local player got the message.
    internal static bool Show(string text, Kind kind)
    {
        Player p = Player.m_localPlayer;
        ModLog.Verbose("Notify: [" + kind + "] " + text + (p == null ? " (no local player - not shown)" : ""));
        if (p == null)
        {
            return p != null;
        }

        TrollMessages.Passing = true;
        try
        {
            p.Message(MessageHud.MessageType.TopLeft, Paint(text, kind));
        }
        finally
        {
            TrollMessages.Passing = false;
        }

        return p != null;
    }

    /// D-058: the big message in the middle of the screen (MessageHud Center, 150 % size, on top of the game view), for the first troll
    /// a player ever tames. Expected outcome: returns whether a local player got it.
    internal static bool Big(string text, Kind kind)
    {
        Player p = Player.m_localPlayer;
        ModLog.Verbose("Notify: BIG [" + kind + "] " + text + (p == null ? " (no local player - not shown)" : ""));
        if (p == null)
        {
            return p != null;
        }

        TrollMessages.Passing = true;
        try
        {
            p.Message(MessageHud.MessageType.Center, "<size=150%>" + Paint(text, kind) + "</size>");
        }
        finally
        {
            TrollMessages.Passing = false;
        }

        return p != null;
    }

    /// D-159: a message in `kind`'s colour at the top of the screen that stays exactly `seconds` (a clone of the MessageHud's centre text, hidden by Tick).
    /// Expected outcome: returns whether it was shown.
    internal static bool Timed(string text, Kind kind, float seconds)
    {
        MessageHud hud = MessageHud.instance;
        ModLog.Verbose("Notify: TIMED " + seconds.ToString("0.#") + " s [" + kind + "] " + text);
        if (hud == null || hud.m_messageText == null || Player.m_localPlayer == null)
        {
            return false;
        }

        if (_timed == null)
        {
            // The centre text lives in the full-screen hud root (the left text sits in the TopLeft area and was clipped there).
            TMP_Text src = hud.m_messageCenterText != null ? hud.m_messageCenterText : hud.m_messageText;
            GameObject go = Object.Instantiate(src.gameObject, hud.transform);
            go.name = "STT_TimedMessage";
            _timed = go.GetComponent<TMP_Text>();
            RectTransform rt = _timed.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(1600f, 60f);
            rt.anchoredPosition = new Vector2(0f, -170f);
            _timed.alignment = TextAlignmentOptions.Top;
            _timed.textWrappingMode = TextWrappingModes.Normal;
            _timed.overflowMode = TextOverflowModes.Overflow;
            _timed.richText = true;
        }

        _timed.text = Paint(text, kind);
        _timed.gameObject.SetActive(true);
        _timedUntil = Time.time + seconds;
        return true;
    }

    /// Hides the timed message when its time is up (called every frame by HubSpeed.Tick).
    internal static void TickTimed()
    {
        if (_timed != null && _timed.gameObject.activeSelf && Time.time >= _timedUntil)
        {
            _timed.gameObject.SetActive(false);
        }
    }

    internal enum Kind
    {
        Info,
        Good,
        Warn,
        Bad,
        Farm,
        Attack,
        Death
    }
}
