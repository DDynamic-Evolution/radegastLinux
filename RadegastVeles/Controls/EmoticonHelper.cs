using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Radegast.Veles.Controls;

/// <summary>
/// Converts text emoticons to Unicode emoji characters.
/// </summary>
public static class EmoticonHelper
{
    private static readonly Dictionary<string, string> EmoticonMap = new()
    {
        // Happy
        { ":)", "😊" }, { ":-)", "😊" }, { "=)", "😊" },
        { ":D", "😄" }, { ":-D", "😄" }, { "=D", "😄" },
        { ":3", "😺" },
        { ":v", "😃" },
        { "B)", "😎" }, { "B-)", "😎" },
        { "o:)", "😇" }, { "O:)", "😇" },
        { ">:)", "😈" },

        // Sad
        { ":(", "😢" }, { ":-(", "😢" }, { "=(", "😢" },
        { ":'(", "😢" },
        { "T_T", "😭" },
        { ":c", "😟" },

        // Surprised
        { ":O", "😮" }, { ":-O", "😮" },
        { "OwO", "😮" },

        // Winking
        { ";)", "😉" }, { ";-)", "😉" },

        // Tongue
        { ":P", "😛" }, { ":-P", "😛" }, { "=P", "😛" },
        { ":p", "😛" }, { ":-p", "😛" }, { "=p", "😛" },

        // Confused/Uncertain
        { ":/", "😕" }, { ":-/", "😕" },
        { ":\\", "😕" }, { ":-\\", "😕" },
        { ":|", "😐" }, { ":-|", "😐" },
        { "-_-", "😑" },

        // Laughing
        { "XD", "😆" }, { "xD", "😆" }, { "xd", "😆" },

        // Love
        { "<3", "❤️" },
        { ":*", "😘" }, { ":-*", "😘" },
        { "UwU", "🥰" }, { "uwu", "🥰" },

        // Angry
        { ">:(", "😡" }, { ">:-( ", "😡" },
        { ">_<", "😣" },
        { ":@" , "😠" },

        // Embarrassed
        { ":$", "😳" },

        // Cute
        { ">:3", "😼" },
    };

    // Sort by length descending to match longer patterns first (e.g., ":-)" before ":)")
    private static readonly List<KeyValuePair<string, string>> SortedEmoticons =
        new(EmoticonMap);

    static EmoticonHelper()
    {
        SortedEmoticons.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
    }

    /// <summary>
    /// Replaces all known emoticons in the text with their Unicode emoji equivalents.
    /// </summary>
    public static string ReplaceEmoticons(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        foreach (var (emoticon, emoji) in SortedEmoticons)
        {
            text = ReplaceEmoticon(text, emoticon, emoji);
        }

        return text;
    }

    private static string ReplaceEmoticon(string text, string emoticon, string emoji)
    {
        int index = 0;
        while ((index = text.IndexOf(emoticon, index, StringComparison.Ordinal)) >= 0)
        {
            // Check word boundaries to avoid replacing emoticons inside URLs or words
            bool validStart = index == 0 || IsBoundaryChar(text[index - 1]);
            bool validEnd = index + emoticon.Length >= text.Length || IsBoundaryChar(text[index + emoticon.Length]);

            if (validStart && validEnd)
            {
                text = text.Substring(0, index) + emoji + text.Substring(index + emoticon.Length);
                index += emoji.Length;
            }
            else
            {
                index += emoticon.Length;
            }
        }

        return text;
    }

    private static bool IsBoundaryChar(char c)
    {
        // Emoticons should be preceded/followed by whitespace, punctuation, or start/end of string
        return char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSeparator(c);
    }

    /// <summary>
    /// Returns a list of all supported emoticons for the picker UI.
    /// </summary>
    public static IReadOnlyList<(string Emoticon, string Emoji)> GetAllEmoticons()
    {
        var result = new List<(string, string)>();
        var seen = new HashSet<string>();

        foreach (var (emoticon, emoji) in SortedEmoticons)
        {
            if (seen.Add(emoji))
                result.Add((emoticon, emoji));
        }

        return result;
    }
}
