/*
Copyright (C) 2007, 2008 Kristian Bisgaard Lassen
Copyright (C) 2010 Kristian Helkjaer Lassen

This library is free software; you can redistribute it and/or
modify it under the terms of the GNU Lesser General Public
License as published by the Free Software Foundation; either
version 3 of the License, or (at your option) any later version.

This library is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public
License along with this library. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using ZoneFiveSoftware.Common.Data.Fitness;
using System.Diagnostics;
using ZoneFiveSoftware.Common.Visuals;
using ZoneFiveSoftware.Common.Visuals.Fitness;
using ZoneFiveSoftware.Common.Data.GPS;
using ZoneFiveSoftware.Common.Data;
using ZoneFiveSoftware.Common.Data.Measurement;
using System.Collections;
using System.IO;
using System.Xml;
using SportTracksUniqueRoutesPlugin.Properties;
using SportTracksUniqueRoutesPlugin.Util;

namespace SportTracksUniqueRoutesPlugin.Source
{
    public partial class UniqueRoutesSettingPageControl : UserControl
    {
        public UniqueRoutesSettingPageControl()
        {
            InitializeComponent();
            updateLanguage();
            bandwidthBox.LostFocus += new EventHandler(bandwidthBox_LostFocus);
            percentageOff.LostFocus += new EventHandler(percentageOff_LostFocus);
            hasDirectionBox.LostFocus += new EventHandler(hasDirectionBox_LostFocus);
            ignoreBeginningBox.LostFocus += new EventHandler(ignoreBeginningBox_LostFocus);
            ignoreEndBox.LostFocus += new EventHandler(ignoreEndBox_LostFocus);            
            presentSettings();            
            Plugin.GetApplication().SystemPreferences.PropertyChanged += new PropertyChangedEventHandler(UniqueRoutesSettingPageControl_PropertyChanged);
        }

        private void precedeControl(Control a, Control b)
        {
            a.Location = new Point(b.Location.X - a.Size.Width - 5, a.Location.Y);
        }

        private void updateLanguage()
        {
            resetSettings.Text = StringResources.ResetAllSettings;
            linkLabel1.Text = Resources.Webpage;
            groupBox1.Text = StringResources.Settings;
            label1.Text = Resources.Bandwidth + ":";
            precedeControl(label1, bandwidthBox);
            label2.Text = Resources.AllowPointsOutsideBand + ":";
            precedeControl(label2, percentageOff);
            label3.Text = Resources.RoutesHaveDirection + ":";
            precedeControl(label3, hasDirectionBox);
            label5.Text = Resources.IgnoreBeginningOfRoute + ":";
            precedeControl(label5, ignoreBeginningBox);
            label8.Text = Resources.IgnoreEndOfRoute + ":";
            precedeControl(label8, ignoreEndBox);
            metricLabel.Text = UnitUtil.Elevation.Label;
            label4.Text = CommonResources.Text.LabelPercent;
            beginningLabel.Text = UnitUtil.Distance.Label;
            endLabel.Text = UnitUtil.Distance.Label;
        }

        private void presentSettings()
        {
            bandwidthBox.Text = UnitUtil.Elevation.ToString(Settings.Bandwidth);
            percentageOff.Value = (int)Math.Round(Settings.ErrorMargin * 100);
            hasDirectionBox.Checked = Settings.HasDirection;
            ignoreBeginningBox.Text = UnitUtil.Distance.ToString(Settings.IgnoreBeginning);
            ignoreEndBox.Text = UnitUtil.Distance.ToString(Settings.IgnoreEnd);

            metricLabel.Text = UnitUtil.Elevation.Label;
            beginningLabel.Text = UnitUtil.Distance.Label;
            endLabel.Text = UnitUtil.Distance.Label;
        }

        void UniqueRoutesSettingPageControl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            presentSettings();
        }
        
        void ignoreEndBox_LostFocus(object sender, EventArgs e)
        {
            try
            {
                double value = UnitUtil.Distance.Parse(ignoreEndBox.Text);
                if (value < 0) { throw new Exception(); }
                Settings.IgnoreEnd = value;
                presentSettings();
            }
            catch (Exception)
            {
                ignoreEndBox.Text = Settings.IgnoreEnd.ToString();
                new WarningDialog(Resources.EndMeterWarning);
            }
        }

        void ignoreBeginningBox_LostFocus(object sender, EventArgs e)
        {
            try
            {
                double value = UnitUtil.Distance.Parse(ignoreBeginningBox.Text);
                if (value < 0) {  throw new Exception(); }
                Settings.IgnoreBeginning = value;
                presentSettings();
            }
            catch (Exception)
            {
                ignoreBeginningBox.Text = Settings.IgnoreBeginning.ToString();
                new WarningDialog(Resources.BeginningMeterWarning);
            }
        }

        private void hasDirectionBox_LostFocus(object sender, EventArgs e)
        {
            Settings.HasDirection = hasDirectionBox.Checked;
            presentSettings();
        }

        private void percentageOff_LostFocus(object sender, EventArgs e)
        {
            Settings.ErrorMargin = (double) percentageOff.Value / 100;
            presentSettings();
        }

        private void bandwidthBox_LostFocus(object sender, EventArgs e)
        {
            try
            {
                int value = (int)UnitUtil.Elevation.Parse(bandwidthBox.Text);
                if (value <= 0) { throw new Exception(); }
                Settings.Bandwidth = value;
                presentSettings();
            }
            catch (Exception)
            {
                bandwidthBox.Text = Settings.Bandwidth.ToString();
                new WarningDialog(Resources.BandwidthWarning);
            }
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(new ProcessStartInfo("IExplore",
                "http://code.google.com/p/gps-running/wiki/UniqueRoutes"));
        }

        private void resetSettings_Click(object sender, EventArgs e)
        {
            presentSettings();
        }

    }
}
