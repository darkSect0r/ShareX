﻿#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2021 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using Manina.Windows.Forms;
using ShareX.HelpersLib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using View = Manina.Windows.Forms.View;

namespace ShareX.HistoryLib
{
    public partial class ImageHistoryForm : Form
    {
        public string HistoryPath { get; private set; }
        public ImageHistorySettings Settings { get; private set; }
        public string SearchText { get; set; }
        public bool SearchInTags { get; set; } = true;

        private HistoryManager history;
        private HistoryItemManager him;
        private string defaultTitle;

        public ImageHistoryForm(string historyPath, ImageHistorySettings settings, Action<string> uploadFile = null, Action<string> editImage = null)
        {
            InitializeComponent();
            tsMain.Renderer = new ToolStripRoundedEdgeRenderer();

            HistoryPath = historyPath;
            Settings = settings;

            ilvImages.View = (View)Settings.ViewMode;
            ilvImages.ThumbnailSize = Settings.ThumbnailSize;

            if (ShareXResources.UseCustomTheme)
            {
                ilvImages.BorderStyle = BorderStyle.None;
                ilvImages.Colors.BackColor = ShareXResources.Theme.DarkBackgroundColor;
                ilvImages.Colors.BorderColor = ShareXResources.Theme.DarkBackgroundColor;
                ilvImages.Colors.ForeColor = ShareXResources.Theme.TextColor;
                ilvImages.Colors.ImageInnerBorderColor = Color.Transparent;
                ilvImages.Colors.ImageOuterBorderColor = Color.Transparent;
                ilvImages.Colors.SelectedForeColor = ShareXResources.Theme.TextColor;
                ilvImages.Colors.UnFocusedForeColor = ShareXResources.Theme.TextColor;
            }

            him = new HistoryItemManager(uploadFile, editImage);
            him.GetHistoryItems += him_GetHistoryItems;
            ilvImages.ContextMenuStrip = him.cmsHistory;

            defaultTitle = Text;

            if (Settings.RememberSearchText)
            {
                tstbSearch.Text = Settings.SearchText;
            }

            ShareXResources.ApplyTheme(this);

            Settings.WindowState.AutoHandleFormState(this);
        }

        private void UpdateTitle(int total, int filtered)
        {
            Text = $"{defaultTitle} (Total: {total:N0} - Filtered: {filtered:N0})";
        }

        private void RefreshHistoryItems()
        {
            UpdateSearchText();
            ilvImages.Items.Clear();
            ImageListViewItem[] ilvItems = GetHistoryItems().Select(hi => new ImageListViewItem(hi.FilePath) { Tag = hi }).ToArray();
            ilvImages.Items.AddRange(ilvItems);
        }

        private void UpdateSearchText()
        {
            SearchText = tstbSearch.Text;

            if (Settings.RememberSearchText)
            {
                Settings.SearchText = SearchText;
            }
            else
            {
                Settings.SearchText = "";
            }
        }

        private IEnumerable<HistoryItem> GetHistoryItems()
        {
            if (history == null)
            {
                history = new HistoryManagerJSON(HistoryPath);
            }

            List<HistoryItem> historyItems = history.GetHistoryItems();
            List<HistoryItem> filteredHistoryItems = new List<HistoryItem>();

            Regex regex = null;

            if (!string.IsNullOrEmpty(SearchText))
            {
                string pattern = Regex.Escape(SearchText).Replace("\\?", ".").Replace("\\*", ".*");
                regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }

            for (int i = historyItems.Count - 1; i >= 0; i--)
            {
                HistoryItem hi = historyItems[i];

                if (!string.IsNullOrEmpty(hi.FilePath) && Helpers.IsImageFile(hi.FilePath) &&
                    (regex == null || regex.IsMatch(hi.FileName) || (SearchInTags && hi.Tags != null && hi.Tags.Any(tag => regex.IsMatch(tag.Value)))) &&
                    (!Settings.FilterMissingFiles || File.Exists(hi.FilePath)))
                {
                    filteredHistoryItems.Add(hi);

                    if (Settings.MaxItemCount > 0 && filteredHistoryItems.Count >= Settings.MaxItemCount)
                    {
                        break;
                    }
                }
            }

            UpdateTitle(historyItems.Count, filteredHistoryItems.Count);

            return filteredHistoryItems;
        }

        private HistoryItem[] him_GetHistoryItems()
        {
            return ilvImages.SelectedItems.Select(x => x.Tag as HistoryItem).ToArray();
        }

        #region Form events

        private void ImageHistoryForm_Shown(object sender, EventArgs e)
        {
            tstbSearch.Focus();
            Application.DoEvents();
            this.ForceActivate();
            RefreshHistoryItems();
        }

        private void ImageHistoryForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5)
            {
                RefreshHistoryItems();
                e.Handled = true;
            }
        }

        private void ilvImages_SelectionChanged(object sender, EventArgs e)
        {
            him.UpdateSelectedHistoryItem();
        }

        private void ilvImages_ItemDoubleClick(object sender, ItemClickEventArgs e)
        {
            him.ShowImagePreview();
        }

        private void tstbSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                RefreshHistoryItems();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void tsbSearch_Click(object sender, EventArgs e)
        {
            RefreshHistoryItems();
        }

        private void tsbSettings_Click(object sender, EventArgs e)
        {
            using (ImageHistorySettingsForm form = new ImageHistorySettingsForm(Settings))
            {
                form.ShowDialog();
            }

            ilvImages.View = (View)Settings.ViewMode;
            ilvImages.ThumbnailSize = Settings.ThumbnailSize;
            RefreshHistoryItems();
        }

        private void ilvImages_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyData)
            {
                default:
                    return;
                case Keys.Enter:
                    him.OpenURL();
                    break;
                case Keys.Control | Keys.Enter:
                    him.OpenFile();
                    break;
                case Keys.Control | Keys.C:
                    him.CopyURL();
                    break;
            }

            e.Handled = true;
        }

        #endregion Form events
    }
}