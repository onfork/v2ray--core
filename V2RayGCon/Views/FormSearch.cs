﻿using ScintillaNET;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace V2RayGCon.Views
{
    public partial class FormSearch : Form
    {
        Scintilla scintilla;
        List<int> result;
        int curResult, keywordLength;


        public FormSearch(Scintilla editor)
        {
            InitializeComponent();
            scintilla = editor;
            result = new List<int>();
            curResult = 0;
            InitIndicator();

            this.FormClosed += (s, a) => ClearIndicator();

#if DEBUG
            this.Icon = Properties.Resources.icon_light;
#endif

            this.Show();
        }

        string GetKeyword()
        {
            return tboxKeyword.Text;
        }

        void InitIndicator()
        {
            // Indicators 0-7 could be in use by a lexer
            // so we'll use indicator 8 to highlight words.
            int NUM = 8;

            // Remove all uses of our indicator
            scintilla.IndicatorCurrent = NUM;
            // scintilla.IndicatorClearRange(0, scintilla.TextLength);

            // Update indicator appearance
            scintilla.Indicators[NUM].Style = IndicatorStyle.StraightBox;
            scintilla.Indicators[NUM].Under = true;
            scintilla.Indicators[NUM].ForeColor = Color.Green;
            scintilla.Indicators[NUM].OutlineAlpha = 50;
            scintilla.Indicators[NUM].Alpha = 30;
        }

        void ClearIndicator()
        {
            scintilla.IndicatorClearRange(0, scintilla.TextLength);
        }


        private void SearchAll()
        {
            var key = GetKeyword();
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            keywordLength = key.Length;

            result.Clear();
            ClearIndicator();
            scintilla.TargetStart = 0;
            scintilla.TargetEnd = scintilla.TextLength;
            while (scintilla.SearchInTarget(key) != -1)
            {
                result.Add(scintilla.TargetStart);
                scintilla.TargetStart = scintilla.TargetEnd;
                scintilla.TargetEnd = scintilla.TextLength;
            }
            curResult = -1;
            ShowResult(true);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyCode)
        {
            switch (keyCode)
            {
                case Keys.F2:
                    ShowResult(false);
                    break;
                case Keys.F3:
                    ShowResult(true);
                    break;
                case Keys.Escape:
                    this.Close();
                    break;
            }

            return base.ProcessCmdKey(ref msg, keyCode);
        }


        void ShowResult(bool forward)
        {
            var count = result.Count;

            if (count < 1)
            {
                return;
            }

            int delta = 1;
            if (!forward)
            {
                delta = -1;
            }
            curResult = (curResult + delta + count) % count;
            scintilla.GotoPosition(result[curResult]);
            scintilla.ScrollCaret();
            ClearIndicator();
            scintilla.IndicatorFillRange(result[curResult], keywordLength);
        }

        private void btnNext_Click(object sender, System.EventArgs e)
        {
            ShowResult(true);
        }

        private void tboxKeyword_TextChanged(object sender, System.EventArgs e)
        {
            var key = GetKeyword();
            if (key.Length > 1)
            {
                SearchAll();
            }
        }

        private void btnPrevious_Click(object sender, System.EventArgs e)
        {
            ShowResult(false);
        }
    }
}
