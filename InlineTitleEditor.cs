using System;
using System.Drawing;
using System.Windows.Forms;

namespace DeskDecor
{
    // A small normal window supplies native text input over the layered desktop
    // bitmap. It has exactly the title's bounds, with no caption or dialog UI.
    internal sealed class InlineTitleEditor : Form
    {
        private readonly TitleTextBox _input;
        private bool _finishing;
        private string _value;
        public bool SaveChanges { get; private set; }
        public string Value { get { return _finishing ? _value : _input.Text.Trim(); } }

        public InlineTitleEditor(string title, Rectangle screenBounds)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            Bounds = screenBounds;
            BackColor = Color.FromArgb(38, 46, 56);
            Padding = new Padding(1);
            _input = new TitleTextBox
            {
                Text = title, MaxLength = 40, BorderStyle = BorderStyle.None,
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Pixel),
                BackColor = BackColor, ForeColor = Color.White,
                Location = new Point(4, Math.Max(2, (ClientSize.Height - 18) / 2)),
                Width = Math.Max(1, ClientSize.Width - 8), TabIndex = 0
            };
            Controls.Add(_input);
            _input.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                // Let the IME accept/cancel its candidate before handling Enter/Escape.
                if (_input.Composing) return;
                if (e.KeyCode != Keys.Enter && e.KeyCode != Keys.Escape) return;
                e.Handled = e.SuppressKeyPress = true;
                Finish(e.KeyCode == Keys.Enter);
            };
            Shown += delegate { _input.Focus(); _input.SelectAll(); };
            Deactivate += delegate { Finish(true); };
        }

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ExStyle |= Native.WS_EX_TOOLWINDOW; return cp; }
        }

        public void Finish(bool save)
        {
            if (_finishing || IsDisposed) return;
            _value = _input.Text.Trim();
            _finishing = true;
            SaveChanges = save;
            Close();
        }

        private sealed class TitleTextBox : TextBox
        {
            public bool Composing { get; private set; }
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x010D) Composing = true; // WM_IME_STARTCOMPOSITION
                if (m.Msg == 0x010E) Composing = false; // WM_IME_ENDCOMPOSITION
                base.WndProc(ref m);
            }
        }
    }
}
