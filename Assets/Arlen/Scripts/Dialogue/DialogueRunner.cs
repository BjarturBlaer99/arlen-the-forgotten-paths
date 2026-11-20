using System;
using System.Collections.Generic;
using Arlen.Player;
using UnityEngine;

namespace Arlen.DialogueSystem
{
    /// <summary>
    /// Plays conversations one line at a time with a typewriter reveal. Blocks gameplay input while
    /// running; Interact/Attack/Jump (the reader's Submit) completes the line, then advances.
    /// Presentation is delegated to <see cref="DialogueView"/>. One runner per scene.
    /// </summary>
    public class DialogueRunner : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] DialogueView view;
        [SerializeField, Min(1f)] float charactersPerSecond = 50f;

        readonly List<DialogueData.Line> lines = new List<DialogueData.Line>(16);
        string defaultSpeaker;
        Action onComplete;
        int index;
        float revealed;
        int lastShownChars = -1;
        int startedFrame = -1;

        public bool IsRunning { get; private set; }
        public int LineIndex => index;
        public bool LineFullyRevealed => IsRunning && revealed >= CurrentText.Length;
        public string CurrentText => IsRunning ? lines[index].text ?? string.Empty : string.Empty;
        public string CurrentSpeaker => IsRunning ? DialogueData.ResolveSpeaker(lines[index].speaker, defaultSpeaker) : string.Empty;

        public event Action Started;
        public event Action Ended;
        /// <summary>(speaker, text) when a new line begins.</summary>
        public event Action<string, string> LineStarted;

        public void Configure(PlayerInputReader reader, DialogueView dialogueView)
        {
            if (isActiveAndEnabled && input != null) input.SubmitPressed -= OnSubmit;
            input = reader;
            view = dialogueView;
            if (isActiveAndEnabled && input != null) input.SubmitPressed += OnSubmit;
        }

        void OnEnable()
        {
            if (input != null) input.SubmitPressed += OnSubmit;
            if (view != null && !IsRunning) view.Hide();
        }

        void OnDisable()
        {
            if (input != null) input.SubmitPressed -= OnSubmit;
            if (IsRunning) Stop();
        }

        public bool Play(DialogueData data, string speakerOverride = null, Action completed = null)
        {
            if (data == null || data.Count == 0) return false;
            lines.Clear();
            lines.AddRange(data.lines);
            return Begin(string.IsNullOrEmpty(speakerOverride) ? data.defaultSpeaker : speakerOverride, completed);
        }

        /// <summary>Plays plain text lines (examine descriptions, signs). Speaker null/"-" = narration.</summary>
        public bool Play(IReadOnlyList<string> textLines, string speaker = DialogueData.Narration, Action completed = null)
        {
            if (textLines == null || textLines.Count == 0) return false;
            lines.Clear();
            for (int i = 0; i < textLines.Count; i++) lines.Add(new DialogueData.Line(string.Empty, textLines[i]));
            return Begin(speaker ?? DialogueData.Narration, completed);
        }

        bool Begin(string speaker, Action completed)
        {
            if (IsRunning) return false;
            defaultSpeaker = speaker;
            onComplete = completed;
            index = 0;
            IsRunning = true;
            startedFrame = Time.frameCount;
            if (input != null) input.PushBlock();
            if (view != null) view.Show();
            Started?.Invoke();
            ShowLine();
            return true;
        }

        /// <summary>Completes the current line, or advances to the next one.</summary>
        public void Advance()
        {
            if (!IsRunning) return;
            if (!LineFullyRevealed)
            {
                revealed = CurrentText.Length;
                Render();
                return;
            }

            index++;
            if (index >= lines.Count) Stop();
            else ShowLine();
        }

        /// <summary>Ends the conversation immediately (still invokes the completion callback).</summary>
        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;
            if (view != null) view.Hide();
            if (input != null) input.PopBlock();
            var callback = onComplete;
            onComplete = null;
            Ended?.Invoke();
            callback?.Invoke();
        }

        void OnSubmit()
        {
            // Ignore the press that opened the conversation, and presses while the game is paused.
            if (!IsRunning || Time.frameCount == startedFrame || Time.timeScale <= 0f) return;
            Advance();
        }

        void ShowLine()
        {
            revealed = 0f;
            lastShownChars = -1;
            LineStarted?.Invoke(CurrentSpeaker, CurrentText);
            if (view != null) view.SetSpeaker(CurrentSpeaker);
            Render();
        }

        void Update()
        {
            if (!IsRunning || Time.timeScale <= 0f) return;
            if (revealed < CurrentText.Length)
            {
                revealed = Mathf.Min(CurrentText.Length, revealed + charactersPerSecond * Time.unscaledDeltaTime);
                Render();
            }
        }

        void Render()
        {
            if (view == null) return;
            int chars = Mathf.FloorToInt(revealed);
            if (chars == lastShownChars) return;
            lastShownChars = chars;
            view.SetText(CurrentText, chars, chars >= CurrentText.Length);
        }
    }
}
