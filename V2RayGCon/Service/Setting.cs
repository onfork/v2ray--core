﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using static V2RayGCon.Lib.StringResource;

namespace V2RayGCon.Service
{
    public class Setting : Model.BaseClass.SingletonService<Setting>
    {
        public event EventHandler<Model.Data.StrEvent> OnLog, OnUpdateNotifierText;
        public event EventHandler OnSysProxyChanged;

        #region Properties
        ConcurrentQueue<string> _logCache = new ConcurrentQueue<string>();
        public string logCache
        {
            get
            {
                return string.Join(Environment.NewLine, _logCache)
                    + System.Environment.NewLine;
            }
            private set
            {
                // keep 200 lines of log
                if (_logCache.Count > 300)
                {
                    var blackHole = "";
                    for (var i = 0; i < 100; i++)
                    {
                        _logCache.TryDequeue(out blackHole);
                    }
                }
                _logCache.Enqueue(value);
            }
        }

        public string curSysProxyUrl
        {
            get
            {
                return Properties.Settings.Default.SysProxyUrl;
            }
            set
            {
                Properties.Settings.Default.SysProxyUrl = value.ToString();
                Properties.Settings.Default.Save();
            }
        }

        public Tuple<bool, string> orgSysProxyInfo = null;

        public bool isShowConfigerToolsPanel
        {
            get
            {
                return Properties.Settings.Default.CfgShowToolPanel == true;
            }
            set
            {
                Properties.Settings.Default.CfgShowToolPanel = value;
                Properties.Settings.Default.Save();
            }
        }

        public int maxLogLines
        {
            get
            {
                int n = Properties.Settings.Default.MaxLogLine;
                return Lib.Utils.Clamp(n, 10, 1000);
            }
            private set { }
        }

        #endregion

        #region public methods
        public List<Model.Data.ServerItem> LoadServerList()
        {
            var empty = new List<Model.Data.ServerItem>();

            List<Model.Data.ServerItem> list = null;
            try
            {
                list = JsonConvert.DeserializeObject
                    <List<Model.Data.ServerItem>>(
                    Properties.Settings.Default.ServerList);

                if (list == null)
                {
                    return empty;
                }
            }
            catch
            {
                return empty;
            }

            // make sure every config of server can be parsed correctly
            for (var i = list.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (JObject.Parse(list[i].config) == null)
                    {
                        list.RemoveAt(i);
                    }
                }
                catch
                {
                    list.RemoveAt(i);
                }
            }

            return list;
        }

        public void SetSystemProxy(string link)
        {
            if (string.IsNullOrEmpty(link))
            {
                return;
            }

            Lib.ProxySetter.setProxy(link, true);
            curSysProxyUrl = link;
            InvokeEventOnSysProxyChanged();
        }

        public void ClearSystemProxy()
        {
            curSysProxyUrl = string.Empty;
            Lib.ProxySetter.setProxy(curSysProxyUrl, false);
            InvokeEventOnSysProxyChanged();
        }

        public void LoadSystemProxy()
        {
            if (!string.IsNullOrEmpty(curSysProxyUrl))
            {
                Lib.ProxySetter.setProxy(curSysProxyUrl, true);
            }
        }

        public void SaveOriginalSystemProxyInfo()
        {
            orgSysProxyInfo = new Tuple<bool, string>(
                Lib.ProxySetter.getProxyState(),
                Lib.ProxySetter.getProxyUrl());
        }

        public void RestoreOriginalSystemProxyInfo()
        {
            Lib.ProxySetter.setProxy(
                orgSysProxyInfo.Item2,
                orgSysProxyInfo.Item1);
        }

        public void SaveFormRect(Form form)
        {
            var key = form.GetType().Name;

            var list = GetWinFormRectList();
            list[key] = new Rectangle(
                form.Left, form.Top, form.Width, form.Height);
            Properties.Settings.Default.WinFormPosList =
                JsonConvert.SerializeObject(list);
            Properties.Settings.Default.Save();
        }

        public void RestoreFormRect(Form form)
        {
            var key = form.GetType().Name;

            var list = GetWinFormRectList();

            if (!list.ContainsKey(key))
            {
                return;
            }

            var rect = list[key];
            var screen = Screen.PrimaryScreen.WorkingArea;

            form.Width = Math.Max(rect.Width, 300);
            form.Height = Math.Max(rect.Height, 200);
            form.Left = Lib.Utils.Clamp(rect.Left, 0, screen.Right - form.Width);
            form.Top = Lib.Utils.Clamp(rect.Top, 0, screen.Bottom - form.Height);
        }

        public void SendLog(string log)
        {
            logCache = log;
            try
            {
                OnLog?.Invoke(this, new Model.Data.StrEvent(log));
            }
            catch { }
        }

        public List<Model.Data.ImportItem> GetGlobalImportItems()
        {
            try
            {
                var items = JsonConvert.DeserializeObject
                    <List<Model.Data.ImportItem>>(
                    Properties.Settings.Default.ImportUrls);

                if (items != null)
                {
                    return items;
                }
            }
            catch { };
            return new List<Model.Data.ImportItem>();
        }

        public void SaveGlobalImportItems(string options)
        {
            Properties.Settings.Default.ImportUrls = options;
            Properties.Settings.Default.Save();
        }

        public List<Model.Data.UrlItem> GetSubscriptionItems()
        {
            try
            {
                var items = JsonConvert.DeserializeObject
                    <List<Model.Data.UrlItem>>(
                    Properties.Settings.Default.SubscribeUrls);

                if (items != null)
                {
                    return items;
                }
            }
            catch { };
            return new List<Model.Data.UrlItem>();
        }

        public void UpdateNotifierText(string title = null)
        {
            var text = string.IsNullOrEmpty(title) ? I18N("Description") : title;
            try
            {
                OnUpdateNotifierText?.Invoke(this, new Model.Data.StrEvent(text));
            }
            catch { }
        }

        public void SaveSubscriptionItems(string options)
        {
            Properties.Settings.Default.SubscribeUrls = options;
            Properties.Settings.Default.Save();
        }

        public void SaveServerList(List<Model.Data.ServerItem> serverList)
        {
            string json = JsonConvert.SerializeObject(serverList);
            Properties.Settings.Default.ServerList = json;
            Properties.Settings.Default.Save();
        }
        #endregion

        #region private method
        Dictionary<string, Rectangle> winFormRectListCache = null;
        Dictionary<string, Rectangle> GetWinFormRectList()
        {
            if (winFormRectListCache != null)
            {
                return winFormRectListCache;
            }

            try
            {
                winFormRectListCache = JsonConvert.DeserializeObject<
                    Dictionary<string, Rectangle>>(
                    Properties.Settings.Default.WinFormPosList);
            }
            catch { }

            if (winFormRectListCache == null)
            {
                winFormRectListCache = new Dictionary<string, Rectangle>();
            }

            return winFormRectListCache;
        }

        void InvokeEventOnSysProxyChanged()
        {
            try
            {
                OnSysProxyChanged?.Invoke(this, EventArgs.Empty);
            }
            catch { }
        }
        #endregion
    }
}
