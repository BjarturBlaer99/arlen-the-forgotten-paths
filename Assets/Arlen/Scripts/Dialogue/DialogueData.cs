using System;
using UnityEngine;

namespace Arlen.DialogueSystem
{
    /// <summary>
    /// One linear conversation, authored as an asset (Create ▸ Arlen ▸ Dialogue). Each line may name its
    /// speaker; an empty speaker uses the conversation's default (usually the NPC), and "-" means narration.
    /// Branching and conditions are intentionally out of scope until the quest system needs them.
    /// </summary>
    [CreateAssetMenu(menuName = "Arlen/Dialogue", fileName = "Dialogue")]
    public class DialogueData : ScriptableObject
    {
        [Serializable]
        public struct Line
        {
            [Tooltip("Empty = conversation default speaker. \"-\" = narration (no name shown).")]
            public string speaker;
            [TextArea(2, 5)] public string text;

            public Line(string speaker, string text)
            {
                this.speaker = speaker;
                this.text = text;
            }
        }

        public const string Narration = "-";

        [Tooltip("Used for lines with an empty speaker.")]
        public string defaultSpeaker;
        public Line[] lines = Array.Empty<Line>();

        public int Count => lines?.Length ?? 0;

        /// <summary>Resolves the display name for a line ("" for narration).</summary>
        public static string ResolveSpeaker(string lineSpeaker, string fallback)
        {
            if (lineSpeaker == Narration) return string.Empty;
            if (!string.IsNullOrEmpty(lineSpeaker)) return lineSpeaker;
            return fallback == Narration ? string.Empty : fallback ?? string.Empty;
        }
    }
}
