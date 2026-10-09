using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AndroidWireless
{
    /// <summary>
    /// 한 칸씩 입력하는 6자리 페어링 코드 입력란.
    /// 숫자를 치면 다음 칸으로, Backspace는 빈 칸이면 이전 칸으로 이동한다.
    /// 어느 칸에서든 붙여넣기(Ctrl/Cmd+V)하면 숫자만 골라 채우고, 6칸이 다 차면 바로 제출한다.
    /// </summary>
    [UxmlElement]
    public partial class PairingCodeInput : VisualElement
    {
        public const int Length = 6;

        /// <summary>6칸이 다 찼거나 Enter를 눌렀을 때.</summary>
        public event Action<string> Submitted;

        private readonly DigitCell[] cells = new DigitCell[Length];
        private double lastPasteTime = -1;

        public PairingCodeInput()
        {
            AddToClassList("aw-code-input");
            for (int i = 0; i < Length; i++)
            {
                var cell = new DigitCell(i);
                cell.RegisterCallback<KeyDownEvent>(OnKeyDown);
                cell.RegisterCallback<ValidateCommandEvent>(OnValidateCommand);
                cell.RegisterCallback<ExecuteCommandEvent>(OnExecuteCommand);
                cells[i] = cell;
                Add(cell);
            }
        }

        public string Code => new string(cells.Select(c => c.Digit).ToArray());
        public bool IsComplete => cells.All(c => c.Digit != '\0');

        public void ClearCode()
        {
            foreach (var cell in cells) cell.Digit = '\0';
        }

        public void FocusFirst() => cells[0].Focus();

        public void SetError(bool error) => EnableInClassList("aw-code-input--error", error);

        private void OnKeyDown(KeyDownEvent evt)
        {
            var cell = (DigitCell)evt.currentTarget;
            int index = cell.Index;

            if (evt.actionKey && evt.keyCode == KeyCode.V)
            {
                Paste(index);
                evt.StopPropagation();
                return;
            }

            switch (evt.keyCode)
            {
                case KeyCode.Backspace:
                    if (cell.Digit != '\0') cell.Digit = '\0';
                    else if (index > 0)
                    {
                        cells[index - 1].Digit = '\0';
                        cells[index - 1].Focus();
                    }
                    SetError(false);
                    evt.StopPropagation();
                    return;
                case KeyCode.Delete:
                    cell.Digit = '\0';
                    evt.StopPropagation();
                    return;
                case KeyCode.LeftArrow:
                    if (index > 0) cells[index - 1].Focus();
                    evt.StopPropagation();
                    return;
                case KeyCode.RightArrow:
                    if (index < Length - 1) cells[index + 1].Focus();
                    evt.StopPropagation();
                    return;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    TrySubmit();
                    evt.StopPropagation();
                    return;
            }

            // 문자 입력은 keyCode 없이 character만 실린 두 번째 KeyDownEvent로 온다.
            char ch = evt.character;
            if (ch >= '0' && ch <= '9')
            {
                cell.Digit = ch;
                SetError(false);
                if (IsComplete) TrySubmit();
                else if (index < Length - 1) cells[index + 1].Focus();
                evt.StopPropagation();
            }
            else if (ch != '\0' && !char.IsControl(ch))
            {
                evt.StopPropagation(); // 숫자 외 문자는 무시
            }
        }

        private void OnValidateCommand(ValidateCommandEvent evt)
        {
            if (evt.commandName == "Paste") evt.StopPropagation();
        }

        private void OnExecuteCommand(ExecuteCommandEvent evt)
        {
            if (evt.commandName != "Paste") return;
            Paste(((DigitCell)evt.currentTarget).Index);
            evt.StopPropagation();
        }

        private void Paste(int from)
        {
            // 단축키 KeyDown과 Paste 명령이 둘 다 올 수 있으므로 한 번만 처리한다.
            double now = EditorApplication.timeSinceStartup;
            if (now - lastPasteTime < 0.25) return;
            lastPasteTime = now;

            string digits = new string((EditorGUIUtility.systemCopyBuffer ?? "").Where(c => c >= '0' && c <= '9').ToArray());
            if (digits.Length == 0) return;

            // 6자리 이상이면 처음부터 전체를, 아니면 현재 칸부터 채운다.
            int start = digits.Length >= Length ? 0 : from;
            int count = Mathf.Min(digits.Length, Length - start);
            for (int i = 0; i < count; i++) cells[start + i].Digit = digits[i];
            SetError(false);

            if (IsComplete)
            {
                cells[Length - 1].Focus();
                TrySubmit();
            }
            else
            {
                cells[Mathf.Min(start + count, Length - 1)].Focus();
            }
        }

        private void TrySubmit()
        {
            if (!enabledInHierarchy || !IsComplete) return;
            Submitted?.Invoke(Code);
        }

        private sealed class DigitCell : VisualElement
        {
            public readonly int Index;
            private readonly Label label;
            private char digit;

            public DigitCell(int index)
            {
                Index = index;
                focusable = true;
                tabIndex = index;
                AddToClassList("aw-digit");
                label = new Label { pickingMode = PickingMode.Ignore };
                label.AddToClassList("aw-digit__label");
                Add(label);
            }

            public char Digit
            {
                get => digit;
                set
                {
                    digit = value;
                    label.text = value == '\0' ? "" : value.ToString();
                    EnableInClassList("aw-digit--filled", value != '\0');
                }
            }
        }
    }
}
