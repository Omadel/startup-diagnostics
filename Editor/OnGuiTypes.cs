using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Which component types have an <c>OnGUI</c> method in the builds: an active one makes the first frame load the
    ///     skin of IMGUI.
    /// </summary>
    internal static class OnGuiTypes
    {
        private const BindingFlags DECLARED = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private const string EDITOR_SYMBOL = "UNITY_EDITOR";

        private static readonly Regex _onGuiDeclaration = new(@"\bvoid\s+OnGUI\s*\(\s*\)", RegexOptions.Compiled);
        private static readonly Regex _directive = new(@"^\s*#\s*(if|elif|else|endif)\b(.*)$", RegexOptions.Compiled);

        /// <summary>
        ///     Whether the type, or one of its base types below <see cref="MonoBehaviour" />, declares <c>OnGUI</c> in the
        ///     editor.
        /// </summary>
        public static bool Declares(Type type)
        {
            return GetDeclaringType(type) != null;
        }

        /// <summary>
        ///     The type, or the base type below <see cref="MonoBehaviour" />, that declares <c>OnGUI</c>, or null.
        /// </summary>
        public static Type GetDeclaringType(Type type)
        {
            for (var current = type; current != null && current != typeof(MonoBehaviour); current = current.BaseType)
            {
                if (current.GetMethod("OnGUI", DECLARED, null, Type.EmptyTypes, null) != null) return current;
            }

            return null;
        }

        /// <summary>
        ///     Whether every <c>OnGUI</c> declared in a C# source lies in a block compiled in the editor only, such as
        ///     <c>#if UNITY_EDITOR</c> or the <c>#else</c> of <c>#if !UNITY_EDITOR</c>: the builds then have no
        ///     <c>OnGUI</c>. False when the source declares none.
        /// </summary>
        public static bool IsEditorOnly(string source)
        {
            // Each open block: whether its active branch compiles in the editor only.
            var blocks = new Stack<(bool EditorOnly, bool Negated)>();
            var declarations = 0;
            foreach (var line in source.Split('\n'))
            {
                var directive = _directive.Match(line);
                if (directive.Success)
                {
                    Apply(blocks, directive.Groups[1].Value, directive.Groups[2].Value.Trim());
                    continue;
                }

                if (!_onGuiDeclaration.IsMatch(line)) continue;

                declarations++;
                if (!IsInEditorOnlyBlock(blocks)) return false;
            }

            return declarations > 0;
        }

        private static void Apply(Stack<(bool EditorOnly, bool Negated)> blocks, string keyword, string condition)
        {
            switch (keyword)
            {
                case "if":
                    var negated = condition.StartsWith("!" + EDITOR_SYMBOL, StringComparison.Ordinal);
                    var editorOnly = !negated && (condition == EDITOR_SYMBOL || condition.StartsWith(EDITOR_SYMBOL + " ", StringComparison.Ordinal) || condition.StartsWith(EDITOR_SYMBOL + "&&", StringComparison.Ordinal));
                    blocks.Push((editorOnly, negated));
                    break;
                case "elif":
                    if (blocks.Count > 0)
                    {
                        var (_, wasNegated) = blocks.Pop();
                        blocks.Push((false, wasNegated));
                    }

                    break;
                case "else":
                    if (blocks.Count > 0)
                    {
                        var (_, wasNegated) = blocks.Pop();
                        blocks.Push((wasNegated, wasNegated));
                    }

                    break;
                case "endif":
                    if (blocks.Count > 0)
                    {
                        blocks.Pop();
                    }

                    break;
            }
        }

        private static bool IsInEditorOnlyBlock(IEnumerable<(bool EditorOnly, bool Negated)> blocks)
        {
            foreach (var (editorOnly, _) in blocks)
            {
                if (editorOnly) return true;
            }

            return false;
        }
    }
}