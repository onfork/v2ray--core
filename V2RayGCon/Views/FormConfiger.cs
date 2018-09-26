﻿using System;
using System.Drawing;
using System.Windows.Forms;
using static V2RayGCon.Lib.StringResource;


namespace V2RayGCon.Views
{
    #region auto hide tools panel
    struct ToolsPanelHandler
    {
        public int span;
        public int tabWidth;

        public Rectangle panel;
        public Rectangle editor;
        public Rectangle page;

        public Model.BaseClass.CancelableTimeout timerHide, timerShow;

        public void Dispose()
        {
            timerHide?.Release();
            timerShow?.Release();
        }
    };
    #endregion

    public partial class FormConfiger : Form
    {
        Controller.Configer configer;
        Service.Setting setting;
        Service.Servers servers;

        FormSearch formSearch;
        ToolsPanelHandler toolsPanelHandler;
        string originalConfigString;
        string formTitle;
        bool isShowPanel;

        public FormConfiger(string originalConfigString = null)
        {
            setting = Service.Setting.Instance;
            servers = Service.Servers.Instance;

            isShowPanel = setting.isShowConfigerToolsPanel;
            formSearch = null;
            InitializeComponent();
            formTitle = this.Text;
            this.originalConfigString = originalConfigString;

#if DEBUG
            this.Icon = Properties.Resources.icon_light;
#endif

            this.Show();
        }

        private void FormConfiger_Shown(object sender, EventArgs e)
        {
            setting.RestoreFormRect(this);

            InitToolsPanel();
            this.configer = InitConfiger();

            UpdateServerMenu();
            SetTitle(configer.GetAlias());
            ToggleToolsPanel(isShowPanel);

            var editor = configer
                .GetComponent<Controller.ConfigerComponet.Editor>()
                .GetEditor();

            editor.Click += OnMouseLeaveToolsPanel;
            servers.OnRequireMenuUpdate += MenuUpdateHandler;

            this.FormClosing += (s, a) =>
            {
                if (!configer.IsConfigSaved())
                {
                    a.Cancel = !Lib.UI.Confirm(
                        I18N("ConfirmCloseWinWithoutSave"));
                }
            };

            this.FormClosed += (s, a) =>
            {
                editor.Click -= OnMouseLeaveToolsPanel;
                servers.OnRequireMenuUpdate -= MenuUpdateHandler;
                setting.SaveFormRect(this);
                toolsPanelHandler.Dispose();
                servers.LazyGC();
            };
        }

        #region UI event handler
        private void ShowLeftPanelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleToolsPanel(true);
        }

        private void HideLeftPanelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleToolsPanel(false);
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            configer.InjectConfigHelper(null);

            switch (Lib.UI.ShowSaveFileDialog(
                StrConst("ExtJson"),
                configer.GetConfigFormated(),
                out string filename))
            {
                case Model.Data.Enum.SaveFileErrorCode.Success:
                    SetTitle(filename);
                    configer.MarkOriginalFile();
                    MessageBox.Show(I18N("Done"));
                    break;
                case Model.Data.Enum.SaveFileErrorCode.Fail:
                    MessageBox.Show(I18N("WriteFileFail"));
                    break;
                case Model.Data.Enum.SaveFileErrorCode.Cancel:
                    // do nothing
                    break;
            }
        }

        private void AddNewServerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Lib.UI.Confirm(I18N("AddNewServer")))
            {
                configer.AddNewServer();
                SetTitle(configer.GetAlias());
            }
        }

        private void LoadJsonToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!configer.IsConfigSaved()
                && !Lib.UI.Confirm(I18N("ConfirmLoadNewServer")))
            {
                return;
            }

            string json = Lib.UI.ShowReadFileDialog(StrConst("ExtJson"), out string filename);

            // user cancelled.
            if (json == null)
            {
                return;
            }

            if (configer.LoadJsonFromFile(json))
            {
                cboxConfigSection.SelectedIndex = 0;
                SetTitle(filename);
            }
            else
            {
                MessageBox.Show(I18N("LoadJsonFail"));
            }
        }

        private void NewWinToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            new FormConfiger();
        }

        private void SearchBoxToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowSearchBox();
        }

        private void SaveConfigStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Lib.UI.Confirm(I18N("ConfirmSaveCurConfig")))
            {
                if (configer.SaveServer())
                {
                    SetTitle(configer.GetAlias());
                }
            }
        }

        private void TabCtrlToolPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (isShowPanel)
            {
                return;
            }

            for (int i = 0; i < tabCtrlToolPanel.TabCount; i++)
            {
                if (tabCtrlToolPanel.GetTabRect(i).Contains(e.Location))
                {
                    if (tabCtrlToolPanel.SelectedIndex != i)
                    {
                        tabCtrlToolPanel.SelectTab(i);
                    }
                    break;
                }
            }
        }

        private void tabCtrlToolPanel_MouseLeave(object sender, EventArgs e)
        {
            toolsPanelHandler.timerShow.Cancel();
        }
        #endregion

        #region bind hotkey
        protected override bool ProcessCmdKey(ref Message msg, Keys keyCode)
        {
            switch (keyCode)
            {
                case (Keys.Control | Keys.P):
                    ToggleToolsPanel(!isShowPanel);
                    break;
                case (Keys.Control | Keys.F):
                    ShowSearchBox();
                    break;
                case (Keys.Control | Keys.S):
                    configer.InjectConfigHelper(null);
                    break;
            }
            return base.ProcessCmdKey(ref msg, keyCode);
        }
        #endregion

        #region init
        Controller.Configer InitConfiger()
        {
            var configer = new Controller.Configer(this.originalConfigString);

            configer
                .Plug(new Controller.ConfigerComponet.EnvVar(
                    cboxImportAlias,
                    tboxImportURL,
                    btnInsertImport,
                    cboxEnvName,
                    tboxEnvValue,
                    btnInsertEnv))
                .Plug(new Controller.ConfigerComponet.Editor(
                    panelScintilla,
                    cboxConfigSection,
                    cboxExamples,
                    btnFormat,
                    btnClearModify))
                .Plug(new Controller.ConfigerComponet.Vmess(
                    tboxVMessID,
                    tboxVMessLevel,
                    tboxVMessAid,
                    tboxVMessIPaddr,
                    rbtnIsServerMode,
                    btnVMessGenUUID,
                    btnVMessInsertClient))
                .Plug(new Controller.ConfigerComponet.VGC(
                    tboxVGCAlias,
                    tboxVGCDesc,
                    btnInsertVGC))
                .Plug(new Controller.ConfigerComponet.StreamSettings(
                    cboxStreamType,
                    cboxStreamParam,
                    rbtnIsServerMode,
                    btnInsertStream,
                    chkStreamUseTls,
                    chkStreamUseSockopt))
                .Plug(new Controller.ConfigerComponet.Shadowsocks(
                    rbtnIsServerMode,
                    tboxSSAddr,
                    tboxSSPassword,
                    chkSSIsShowPassword,
                    cboxSSMethod,
                    chkSSIsUseOTA,
                    btnInsertSSSettings))
                .Plug(new Controller.ConfigerComponet.Import(
                    panelExpandConfig,
                    cboxGlobalImport,
                    btnExpandImport,
                    btnImportClearCache,
                    btnCopyExpansedConfig))
                .Plug(new Controller.ConfigerComponet.Quick(
                    btnQConSkipCN,
                    btnQConMTProto));

            configer.Prepare();
            return configer;
        }

        void ExpanseToolsPanel()
        {
            var width = toolsPanelHandler.panel.Width;
            if (pnlTools.Width != width)
            {
                pnlTools.Invoke((MethodInvoker)delegate
                {
                    pnlTools.Width = width;
                });
            }
        }

        void InitToolsPanel()
        {
            toolsPanelHandler.editor = new Rectangle(pnlEditor.Location, pnlEditor.Size);
            toolsPanelHandler.panel = new Rectangle(pnlTools.Location, pnlTools.Size);

            toolsPanelHandler.span = (ClientRectangle.Width - toolsPanelHandler.panel.Width - toolsPanelHandler.editor.Width) / 3;
            toolsPanelHandler.tabWidth = tabCtrlToolPanel.Left + tabCtrlToolPanel.ItemSize.Width;

            toolsPanelHandler.timerHide = new Model.BaseClass.CancelableTimeout(FoldToolsPanel, 800);
            toolsPanelHandler.timerShow = new Model.BaseClass.CancelableTimeout(ExpanseToolsPanel, 500);

            var page = tabCtrlToolPanel.TabPages[0];
            toolsPanelHandler.page = new Rectangle(
                pnlTools.Location.X + page.Left,
                pnlTools.Location.Y + page.Top,
                page.Width,
                page.Height);

            for (int i = 0; i < tabCtrlToolPanel.TabCount; i++)
            {
                tabCtrlToolPanel.TabPages[i].MouseEnter += OnMouseEnterToolsPanel;
                tabCtrlToolPanel.TabPages[i].MouseLeave += (s, e) =>
                {
                    var rect = toolsPanelHandler.page;
                    rect.Height = tabCtrlToolPanel.TabPages[0].Height;
                    if (!rect.Contains(PointToClient(Cursor.Position)))
                    {
                        OnMouseLeaveToolsPanel(s, e);
                    }
                };
            }

            tabCtrlToolPanel.MouseLeave += OnMouseLeaveToolsPanel;
            tabCtrlToolPanel.MouseEnter += OnMouseEnterToolsPanel;
        }
        #endregion

        #region private method

        void SetTitle(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                this.Text = formTitle;
            }
            else
            {
                this.Text = string.Format("{0} - {1}", formTitle, name);
            }
        }

        void UpdateServerMenu()
        {
            var menuReplaceServer = replaceExistServerToolStripMenuItem.DropDownItems;
            var menuLoadServer = loadServerToolStripMenuItem.DropDownItems;

            menuReplaceServer.Clear();
            menuLoadServer.Clear();

            var serverList = servers.GetServerList();

            var enable = serverList.Count > 0;
            replaceExistServerToolStripMenuItem.Enabled = enable;
            loadServerToolStripMenuItem.Enabled = enable;

            for (int i = 0; i < serverList.Count; i++)
            {
                var name = string.Format("{0}.{1}", i + 1, serverList[i].name);
                var org = serverList[i].config;
                menuReplaceServer.Add(new ToolStripMenuItem(name, null, (s, a) =>
                {
                    if (Lib.UI.Confirm(I18N("ReplaceServer")))
                    {
                        if (configer.ReplaceServer(org))
                        {
                            SetTitle(configer.GetAlias());
                        }
                    }
                }));

                menuLoadServer.Add(new ToolStripMenuItem(name, null, (s, a) =>
                {
                    if (!configer.IsConfigSaved()
                    && !Lib.UI.Confirm(I18N("ConfirmLoadNewServer")))
                    {
                        return;
                    }
                    configer.LoadServer(org);
                    SetTitle(configer.GetAlias());
                }));
            }
        }

        void ToggleToolsPanel(bool visible)
        {
            var formSize = this.ClientSize;
            var editorSize = pnlEditor.Size;

            pnlTools.Visible = false;
            pnlEditor.Visible = false;

            if (visible)
            {
                pnlTools.Width = toolsPanelHandler.panel.Width;
                pnlEditor.Left = pnlTools.Left + pnlTools.Width + toolsPanelHandler.span;
                pnlEditor.Width = this.ClientSize.Width - pnlEditor.Left - toolsPanelHandler.span;
            }
            else
            {
                pnlTools.Width = toolsPanelHandler.tabWidth;
                pnlEditor.Left = pnlTools.Left + pnlTools.Width;
                pnlEditor.Width = this.ClientSize.Width - pnlEditor.Left - toolsPanelHandler.span;
            }

            pnlTools.Visible = true;
            pnlEditor.Visible = true;

            showLeftPanelToolStripMenuItem.Checked = visible;
            hideLeftPanelToolStripMenuItem.Checked = !visible;

            setting.isShowConfigerToolsPanel = visible;
            isShowPanel = visible;
        }

        void MenuUpdateHandler(object sender, EventArgs args)
        {
            try
            {
                mainMenu.Invoke((MethodInvoker)delegate
                {
                    UpdateServerMenu();
                });
            }
            catch { }
        }

        void OnMouseEnterToolsPanel(object sender, EventArgs e)
        {
            toolsPanelHandler.timerHide.Cancel();
            toolsPanelHandler.timerShow.Start();
        }

        void FoldToolsPanel()
        {
            var visible = isShowPanel;
            var width = toolsPanelHandler.panel.Width;

            if (!visible)
            {
                width = toolsPanelHandler.tabWidth;
            }

            if (pnlTools.Width != width)
            {
                pnlTools.Invoke((MethodInvoker)delegate
                {
                    pnlTools.Width = width;
                });
            }
        }

        void OnMouseLeaveToolsPanel(object sender, EventArgs e)
        {
            toolsPanelHandler.timerShow.Cancel();
            toolsPanelHandler.timerHide.Start();
        }

        void ShowSearchBox()
        {
            if (formSearch != null)
            {
                return;
            }
            var editor = configer.GetComponent<Controller.ConfigerComponet.Editor>();
            formSearch = new FormSearch(editor.GetEditor());
            formSearch.FormClosed += (s, a) => formSearch = null;
        }
        #endregion
    }
}
