// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="StringExtensions.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace BackupManager.Extensions;

/// <summary>
///     Extension methods for the <see cref="string" /> class.
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
internal static partial class StringExtensions
{
    public static bool ContainsInvalidPathChars(this string text)
    {
        if (text.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(text));

        return text.IndexOfAny(Path.GetInvalidPathChars()) >= 0;
    }

    [DebuggerStepThrough]
    public static bool ContainsAny(this string haystack, params string[] needles)
    {
        return needles.Any(haystack.Contains);
    }

    public static bool EndsWithAny(this string haystack, params string[] needles)
    {
        return needles.Any(haystack.EndsWith);
    }

    public static bool EqualsAnyIgnoreCase(this string haystack, params string[] needles)
    {
        return needles.Any(haystack.EqualsIgnoreCase);
    }

    [DebuggerStepThrough]
    public static string WrapInSquareBrackets(this string text)
    {
        return $"[{text}]";
    }

    /// <summary>
    ///     Returns the TitleCase of the string but fixes 10Th to 10th etc.
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static string ToTitleCaseIgnoreOrdinals(this string text)
    {
        var input = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text);
        var result = MyRegex().Replace(input, static m => m.Captures[0].Value.ToLower());
        return result;
    }

    /// <summary>
    ///     Capitalizes the first character of the specified string. Doesn't change the rest of the string.
    /// </summary>
    /// <param name="s">
    ///     The string to capitalize.
    /// </param>
    /// <returns>
    ///     A string with the first character capitalized.
    /// </returns>
    public static string Capitalize(this string s)
    {
        if (s.HasNoValue()) return string.Empty;

        var chars = s.ToCharArray();
        chars[0] = char.ToUpperInvariant(chars[0]);
        return new string(chars);
    }

    /// <summary>
    ///     Replaces the format item in a <see cref="string" /> with the text equivalent of the value of a corresponding
    ///     <see cref="object" />
    ///     instance in a specified array, using the <see cref="CultureInfo.InvariantCulture" />.
    /// </summary>
    /// <param name="format">
    ///     A composite format string.
    /// </param>
    /// <param name="args">
    ///     An <see cref="object" /> array containing zero or more objects to format.
    /// </param>
    /// <returns>
    ///     A copy of format in which the format items have been replaced by the <see cref="string" /> equivalent of the
    ///     corresponding instances
    ///     of <see cref="object" />  in args.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Either <paramref name="format" /> or <paramref name="args" /> is <c>Null</c>.
    /// </exception>
    /// <exception cref="FormatException">
    ///     <paramref name="format" />is invalid.
    ///     <para>
    ///         - or -.
    ///     </para>
    ///     The number indicating an argument to format is less than zero, or greater than or equal to the length of the
    ///     <paramref name="args" /> array.
    /// </exception>
    public static string FormatWith(this string format, params object[] args)
    {
        return string.Format(CultureInfo.InvariantCulture, format, args);
    }

    /// <summary>
    ///     Replaces the format item in a <see cref="string" /> with the text equivalent of the value of a corresponding
    ///     <see cref="object" />
    ///     instance in a specified array. A specified parameter supplies culture-specific formatting information.
    /// </summary>
    /// <param name="format">
    ///     A composite format string.
    /// </param>
    /// <param name="provider">
    ///     An <see cref="IFormatProvider" /> that supplies culture-specific formatting information.
    /// </param>
    /// <param name="args">
    ///     An <see cref="object" /> array containing zero or more objects to format.
    /// </param>
    /// <returns>
    ///     A copy of format in which the format items have been replaced by the <see cref="string" /> equivalent of the
    ///     corresponding instances
    ///     of <see cref="object" />  in args.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Either <paramref name="format" /> or <paramref name="args" /> is <c>Null</c>.
    /// </exception>
    /// <exception cref="FormatException">
    ///     <paramref name="format" />is invalid.
    ///     <para>
    ///         - or -.
    ///     </para>
    ///     The number indicating an argument to format is less than zero, or greater than or equal to the length of the
    ///     <paramref name="args" /> array.
    /// </exception>
    public static string FormatWith(this string format, IFormatProvider provider, params object[] args)
    {
        return string.Format(provider, format, args);
    }

    /// <summary>
    ///     Indicates whether the specified <see cref="string" /> is null or an <see cref="string.Empty" /> string.
    /// </summary>
    /// <param name="s">
    ///     The <see cref="string" /> to check.
    /// </param>
    /// <returns>
    ///     <c>false</c> if the value is null or an <see cref="string.Empty" /> string; otherwise, <c>true</c>.
    /// </returns>
    [DebuggerStepThrough]
    public static bool HasValue(this string s)
    {
        return !string.IsNullOrEmpty(s);
    }

    /// <summary>
    ///     Indicates whether the specified <see cref="string" /> is null or an <see cref="string.Empty" /> string.
    /// </summary>
    /// <param name="s">
    ///     The <see cref="string" /> to check.
    /// </param>
    /// <returns>
    ///     <c>true</c> if the value is null or an <see cref="string.Empty" /> string; otherwise, <c>false</c>.
    /// </returns>
    [DebuggerStepThrough]
    public static bool HasNoValue(this string s)
    {
        return string.IsNullOrEmpty(s);
    }

    /// <summary>
    ///     Indicates whether the regular expression finds a match in the input string, using the regular expression specified.
    /// </summary>
    /// <param name="s">
    ///     The string to search for a match.
    /// </param>
    /// <param name="regex">
    ///     The regular expression pattern to match.
    /// </param>
    /// <returns>
    ///     <c>true</c> if the regular expression finds a match; otherwise <c>false</c>.
    /// </returns>
    public static bool IsMatch(this string s, string regex)
    {
        return Regex.IsMatch(s, regex);
    }

    /// <summary>
    ///     Indicates whether the regular expression finds a match in the input string, using the regular expression specified
    ///     and the matching options supplied in the options parameter.
    /// </summary>
    /// <param name="s">
    ///     The string to search for a match.
    /// </param>
    /// <param name="regex">
    ///     The regular expression pattern to match.
    /// </param>
    /// <param name="options">
    ///     The regular expression options.
    ///     A bitwise OR combination of <see cref="RegexOptions" /> enumeration values.
    /// </param>
    /// <returns>
    ///     <c>true</c> if the regular expression finds a match; otherwise <c>false</c>.
    /// </returns>
    public static bool IsMatch(this string s, string regex, RegexOptions options)
    {
        return Regex.IsMatch(s, regex, options);
    }

    /// <summary>
    ///     Concatenates a specified separator string between each element of a specified sequence, yielding a single
    ///     concatenated string.
    /// </summary>
    /// <param name="source">
    ///     The sequence of strings to join.
    /// </param>
    /// <param name="separator">
    ///     The separator to join the strings with.
    /// </param>
    /// <returns>
    ///     A single concatenated string.
    /// </returns>
    public static string JoinWith(this IEnumerable<string> source, string separator)
    {
        return string.Join(separator, source.ToArray());
    }

    /// <summary>
    ///     Retrieves a substring after the first instance of a character.
    /// </summary>
    /// <param name="s">
    ///     The string to retrieve the substring from.
    /// </param>
    /// <param name="c">
    ///     The character to seek.
    /// </param>
    /// <returns>
    ///     The substring after the first occurrence of <paramref name="c" />, or <paramref name="s" /> if
    ///     <paramref name="c" /> is not found.
    /// </returns>
    public static string SubstringAfter(this string s, char c)
    {
        if (s.HasNoValue()) return string.Empty;

        var index = s.IndexOf(c);
        return index == -1 ? s : index < s.Length - 1 ? s[(index + 1)..] : string.Empty;
    }

    public static bool StartsWith(this string s, IEnumerable<string> values)
    {
        return values.Any(s.StartsWith);
    }

    /// <summary>
    ///     Retrieves a substring after the first instance of a string.
    /// </summary>
    /// <param name="s">
    ///     The string to retrieve the substring from.
    /// </param>
    /// <param name="value">
    ///     The string to seek.
    /// </param>
    /// <param name="comparisonType">
    ///     The type of comparison to perform.
    /// </param>
    /// <returns>
    ///     The substring after the first occurrence of <paramref name="value" />, or <paramref name="s" /> if
    ///     <paramref name="value" /> is not found.
    /// </returns>
    public static string SubstringAfter(this string s, string value, StringComparison comparisonType)
    {
        var index = s.IndexOf(value, comparisonType);
        return index == -1 ? s : index < s.Length - 1 ? s[(index + value.Length)..] : string.Empty;
    }

    public static string SubstringAfterIgnoreCase(this string s, string value)
    {
        var index = s.IndexOf(value, StringComparison.InvariantCultureIgnoreCase);
        return index == -1 ? s : index < s.Length - 1 ? s[(index + value.Length)..] : string.Empty;
    }

    public static string SubstringAfterLastIgnoreCase(this string s, string value)
    {
        var index = s.LastIndexOf(value, StringComparison.InvariantCultureIgnoreCase);
        return index == -1 ? s : index < s.Length - 1 ? s[(index + value.Length)..] : string.Empty;
    }

    /// <summary>
    ///     Retrieves a substring after the last instance of a character.
    /// </summary>
    /// <param name="s">
    ///     The string to retrieve the substring from.
    /// </param>
    /// <param name="c">
    ///     The character to seek.
    /// </param>
    /// <returns>
    ///     The substring after the last occurrence of <paramref name="c" />, or <paramref name="s" /> if <paramref name="c" />
    ///     is not found.
    /// </returns>
    [DebuggerStepThrough]
    public static string SubstringAfterLast(this string s, char c)
    {
        var index = s.LastIndexOf(c);
        return index == -1 ? s : index < s.Length - 1 ? s[(index + 1)..] : string.Empty;
    }

    /// <summary>
    ///     Retrieves a substring before the first instance of a character.
    /// </summary>
    /// <param name="s">
    ///     The string to retrieve the substring from.
    /// </param>
    /// <param name="c">
    ///     The character to seek.
    /// </param>
    /// <returns>
    ///     The substring before the first occurrence of <paramref name="c" />, or <paramref name="s" /> if
    ///     <paramref name="c" /> is not found.
    /// </returns>
    [DebuggerStepThrough]
    public static string SubstringBefore(this string s, char c)
    {
        var index = s.IndexOf(c);
        return index != -1 ? s[..index] : s;
    }

    /// <summary>
    ///     Retrieves a substring before the first instance of a string.
    /// </summary>
    /// <param name="s">
    ///     The string to retrieve the substring from.
    /// </param>
    /// <param name="value">
    ///     The value to seek.
    /// </param>
    /// <param name="comparisonType">
    ///     The type of comparison to perform.
    /// </param>
    /// <returns>
    ///     The substring before the first occurrence of <paramref name="value" />, or <paramref name="s" /> if
    ///     <paramref name="value" /> is not found.
    /// </returns>
    [DebuggerStepThrough]
    public static string SubstringBefore(this string s, string value, StringComparison comparisonType)
    {
        var index = s.IndexOf(value, comparisonType);
        return index != -1 ? s[..index] : s;
    }

    [DebuggerStepThrough]
    public static string SubstringBeforeIgnoreCase(this string s, string value)
    {
        var index = s.IndexOf(value, StringComparison.InvariantCultureIgnoreCase);
        return index != -1 ? s[..index] : s;
    }

    /// <summary>
    ///     Retrieves a substring before the last instance of a character.
    /// </summary>
    /// <param name="s">
    ///     The string to retrieve the substring from.
    /// </param>
    /// <param name="c">
    ///     The character to seek.
    /// </param>
    /// <returns>
    ///     The substring before the last occurrence of <paramref name="c" />, or <paramref name="s" /> if
    ///     <paramref name="c" /> is not found.
    /// </returns>
    [DebuggerStepThrough]
    public static string SubstringBeforeLast(this string s, char c)
    {
        var index = s.LastIndexOf(c);
        return index != -1 ? s[..index] : s;
    }

    [DebuggerStepThrough]
    public static string SubstringBeforeLastIgnoreCase(this string s, string v)
    {
        var index = s.LastIndexOf(v, StringComparison.InvariantCultureIgnoreCase);
        return index != -1 ? s[..index] : s;
    }

    /// <summary>
    ///     Retrieves a substring before the last instance of a string.
    /// </summary>
    /// <param name="s">
    ///     The string to retrieve the substring from.
    /// </param>
    /// <param name="value">
    ///     The string to seek.
    /// </param>
    /// <param name="comparisonType">
    ///     The type of comparison to perform.
    /// </param>
    /// <returns>
    ///     The substring before the last occurrence of <paramref name="value" />, or <paramref name="s" /> if
    ///     <paramref name="value" /> is not found.
    /// </returns>
    [DebuggerStepThrough]
    public static string SubstringBeforeLast(this string s, string value, StringComparison comparisonType)
    {
        var index = s.LastIndexOf(value, comparisonType);
        return index != -1 ? s[..index] : s;
    }

    /// <summary>
    ///     Truncates a string to a specified length, suffixing the truncated string with an ellipsis (…).
    /// </summary>
    /// <param name="s">
    ///     The string to truncate.
    /// </param>
    /// <param name="length">
    ///     The maximum length of the truncated string, including the ellipsis.
    /// </param>
    /// <returns>
    ///     The truncated string.
    /// </returns>
    public static string Truncate(this string s, int length)
    {
        return s.Length > length ? s[..(length - 1)] + "…" : s;
    }

    [DebuggerStepThrough]
    public static bool IsNullOrWhiteSpace(this string text)
    {
        return string.IsNullOrWhiteSpace(text);
    }

    [DebuggerStepThrough]
    public static bool IsNotNullOrWhiteSpace(this string text)
    {
        return !string.IsNullOrWhiteSpace(text);
    }

    [DebuggerStepThrough]
    public static bool StartsWithIgnoreCase(this string text, string startsWith)
    {
        return text.StartsWith(startsWith, StringComparison.InvariantCultureIgnoreCase);
    }

    [DebuggerStepThrough]
    public static bool EndsWithIgnoreCase(this string text, string endsWith)
    {
        return text.EndsWith(endsWith, StringComparison.InvariantCultureIgnoreCase);
    }

    [DebuggerStepThrough]
    public static bool EqualsIgnoreCase(this string text, string equals)
    {
        return text.Equals(equals, StringComparison.InvariantCultureIgnoreCase);
    }

    public static bool ContainsIgnoreCase(this string text, string contains)
    {
        return text.IndexOf(contains, StringComparison.InvariantCultureIgnoreCase) > -1;
    }

    public static bool Contains(this string source, string toCheck, StringComparison comp)
    {
        return source?.IndexOf(toCheck, comp) >= 0;
    }

    [GeneratedRegex("([0-9]st)|([0-9]th)|([0-9]rd)|([0-9]nd)", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex MyRegex();
}