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
using System.Reflection;

using ZoneFiveSoftware.Common.Data;
using ZoneFiveSoftware.Common.Data.Fitness;
using ZoneFiveSoftware.Common.Visuals;
using ZoneFiveSoftware.Common.Visuals.Chart;
using ZoneFiveSoftware.Common.Visuals.Util;
using ZoneFiveSoftware.Common.Visuals.Fitness;
using ZoneFiveSoftware.Common.Data.Measurement;
using ZoneFiveSoftware.Common.Visuals.Mapping;
using GpsRunningPlugin.Properties;
using GpsRunningPlugin.Util;
using TrailsPlugin;
using TrailsPlugin.Data;
using TrailsPlugin.Utils;
using TrailsPlugin.UI.MapLayers;

namespace GpsRunningPlugin.Source
{
    public partial class PerformancePredictorView : UserControl
    {
        private IActivity lastActivity = null;
#if ST_2_1
        private const object m_DetailPage = null;
#else
        private IDetailPage m_DetailPage = null;
        private IDailyActivityView m_view = null;
        private TrailPointsLayer m_layer = null;
#endif

       public PerformancePredictorView()
        {
            InitializeComponent();
            //InitControls();

            setSize();
            chart.YAxis.Formatter = new Formatter.SecondsToTime();
            chart.XAxis.Formatter = new Formatter.General(UnitUtil.Distance.DefaultDecimalPrecision);
            //Remove this listener - let user explicitly update after changing settings, to avoid crashes
            //Settings.DistanceChanged += new PropertyChangedEventHandler(Settings_DistanceChanged);
        }

        void InitControls(IDetailPage detailPage, IDailyActivityView view, TrailPointsLayer layer)
        {
#if !ST_2_1
            m_DetailPage = detailPage;
            m_view = view;
            m_layer = layer;
#endif

            cameronSeries = new ChartDataSeries(chart, chart.YAxis);
            riegelSeries = new ChartDataSeries(chart, chart.YAxis);

            dataGrid.CellDoubleClick += new DataGridViewCellEventHandler(selectedRow_DoubleClick);
            dataGrid.CellMouseClick += new DataGridViewCellMouseEventHandler(dataGrid_CellMouseClick); 
            this.dataGrid.EnableHeadersVisualStyles = false;
            this.dataGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.dataGrid.RowsDefaultCellStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            this.dataGrid.AdvancedColumnHeadersBorderStyle.All = DataGridViewAdvancedCellBorderStyle.Outset;
        }

        public void ThemeChanged(ITheme visualTheme)
        {
            //RefreshPage();
            //m_visualTheme = visualTheme;
            this.chart.ThemeChanged(visualTheme);
            //Set color for non ST controls
            this.splitContainer1.Panel1.BackColor = visualTheme.Control;
            this.splitContainer1.Panel2.BackColor = visualTheme.Control;

            this.dataGrid.BackgroundColor = visualTheme.Control;
            this.dataGrid.GridColor = visualTheme.Border;
            this.dataGrid.DefaultCellStyle.BackColor = visualTheme.Window;
            this.dataGrid.ColumnHeadersDefaultCellStyle.BackColor = visualTheme.SubHeader;
        }

        public void UICultureChanged(System.Globalization.CultureInfo culture)
        {
        }

        private IList<IActivity> activities = new List<IActivity>();
        public IList<IActivity> Activities
        {
            get { return activities; }
            set
            {
                bool showPage = _showPage;
                _showPage = false;

                //Make sure activities is not null
                if (null == value) { activities.Clear(); }
                else { activities = value; }

                //Reset settings
                if (lastActivity != null)
                {
                    if (lastActivity != null && (activities.Count != 1 || lastActivity != activities[0]))
                    {
#if ST_2_1
                    lastActivity.DataChanged -= new ZoneFiveSoftware.Common.Data.NotifyDataChangedEventHandler(dataChanged);
#else
                        lastActivity.PropertyChanged -= new PropertyChangedEventHandler(Activity_PropertyChanged);
#endif
                    }
                }
                if (1 == activities.Count && activities[0] != null)
                {
                    if (lastActivity != activities[0])
                    {
                        lastActivity = activities[0];
#if ST_2_1
                        lastActivity.DataChanged += new ZoneFiveSoftware.Common.Data.NotifyDataChangedEventHandler(dataChanged);
#else
                        lastActivity.PropertyChanged += new PropertyChangedEventHandler(Activity_PropertyChanged);
#endif
                    }
                }
                else
                {
                    lastActivity = null;
                }

                _showPage = showPage;
                makeData();
                m_layer.ClearOverlays();
            }
        }

#if ST_2_1
        private void dataChanged(object sender, ZoneFiveSoftware.Common.Data.NotifyDataChangedEventArgs e)
        {
            makeData();
        }
#else
        private void Activity_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            makeData();
        }
#endif
        private ChartDataSeries cameronSeries;// = new ChartDataSeries(chart, chart.YAxis);
        private ChartDataSeries riegelSeries;// = new ChartDataSeries(chart, chart.YAxis);
        private DataTable cameronSet = new DataTable();
        private DataTable riegelSet = new DataTable();

        private bool _showPage = false;
        public bool HidePage()
        {
            _showPage = false;
            if (m_layer != null)
            {
                m_layer.ClearOverlays();
                m_layer.HidePage();
            }
            return true;
        }
        public void ShowPage(string bookmark)
        {
            bool changed = (_showPage != true);
            _showPage = true;
            if (changed) { makeData(); }
            if (m_layer != null)
            {
                m_layer.ShowPage(bookmark);
            }
        }

        public void setSize()
        {
            if (dataGrid.Columns.Count > 0 && dataGrid.Rows.Count > 0)
            {
                foreach (DataGridViewColumn column in dataGrid.Columns)
                {
                    if (column.Name.Equals(ActivityIdColumn))
                    {
                        column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                        column.Width = 0;
                        column.Visible = false;
                    }
                    else
                    {
                        column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                        //column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    }
                }
            }
        }

        private const string ActivityIdColumn = "ActivityId";
        public void setView()
        {
            if (_showPage)
            {
                bool showPage = _showPage;
                _showPage = false;
                dataGrid.Visible = false;
                chart.Visible = false;
                _showPage = showPage;
            }
        }

        private void makeData()
        {
            if (_showPage)
            {
                setView();

                bool showPage = _showPage;
                _showPage = false;

                cameronSet.Clear(); cameronSet.Rows.Clear(); cameronSeries.Points.Clear();
                riegelSet.Clear(); riegelSet.Rows.Clear(); riegelSeries.Points.Clear();

                if (activities.Count > 1 || (activities.Count == 1 && ChkHighScore))
                {
                    //Predict using one or many activities (check done that HS enabled prior)
                    makeData(cameronSet, cameronSeries, Cameron);
                    makeData(riegelSet, riegelSeries, Riegel);
                }
                else if (activities.Count == 1)
                {
                    //Predict and training info
                    ActivityInfo info = ActivityInfoCache.Instance.GetInfo(activities[0]);

                    if (info.DistanceMeters > 0 && info.Time.TotalSeconds > 0)
                    {
                        makeData(cameronSet, cameronSeries, Cameron,
                            info.DistanceMeters, info.Time.TotalSeconds);
                        makeData(riegelSet, riegelSeries, Riegel,
                            info.DistanceMeters, info.Time.TotalSeconds);
                    }
                }
                //else: no activity selected
                _showPage = showPage;

                setData();
                setSize();
            }
        }

        private void setData()
        {
            bool showPage = _showPage;
            _showPage = false;
            DataTable table = null;
            ChartDataSeries series = null;
            switch (Settings.Model)
            {
                default:
                case PredictionModel.DAVE_CAMERON:
                    table = cameronSet;
                    series = cameronSeries;
                    break;
                case PredictionModel.PETE_RIEGEL:
                    table = riegelSet;
                    series = riegelSeries;
                    break;
            }
            //if (table.Rows.Count > 0 && series.Points.Count > 0)
            //{

            dataGrid.DataSource = table;
            if (chart != null && !chart.IsDisposed)
            {
                chart.DataSeries.Clear();
                chart.DataSeries.Add(series);
                chart.AutozoomToData(true);
                chart.XAxis.Label = UnitUtil.Distance.LabelAxis;
                chart.YAxis.Label = UnitUtil.Time.LabelAxis;
            }
            _showPage = showPage;
            updateChartVisibility();
        }

        PredictTime Cameron = delegate(double new_dist, double old_dist, double old_time)
                    {
                        double a = 13.49681 - (0.000030363 * old_dist)
                            + (835.7114 / Math.Pow(old_dist, 0.7905));
                        double b = 13.49681 - (0.000030363 * new_dist)
                            + (835.7114 / Math.Pow(new_dist, 0.7905));
                        double new_time = (old_time / old_dist) * (a / b) * new_dist;
                        return new_time;
                    };

        PredictTime Riegel = delegate(double new_dist, double old_dist, double old_time)
                    {
                        double new_time = old_time * Math.Pow(new_dist / old_dist, 1.06);
                        return new_time;
                    };

        private void makeData(DataTable set, ChartDataSeries series,
            PredictTime predict, System.Windows.Forms.ProgressBar progressBar)
        {
            set.Clear();
            set.Columns.Clear();
            set.Columns.Add(UnitUtil.Distance.LabelAxis);
            set.Columns.Add(CommonResources.Text.LabelDistance);
            set.Columns.Add(Resources.PredictedTime, typeof(TimeSpan));
            if (Settings.ShowPace)
            {
                set.Columns.Add(UnitUtil.Pace.LabelAxis);
            }
            else
            {
                set.Columns.Add(UnitUtil.Speed.LabelAxis, typeof(double));
            }
            set.Columns.Add(Resources.UsedActivityStartDate);
            set.Columns.Add(Resources.UsedActivityStartTime);
            set.Columns.Add(Resources.UsedTimeOfActivity, typeof(TimeSpan));
            set.Columns.Add(Resources.StartOfPart + UnitUtil.Distance.LabelAbbr2);
            set.Columns.Add(Resources.UsedLengthOfActivity + UnitUtil.Distance.LabelAbbr2);
            set.Columns.Add(ActivityIdColumn);
            series.Points.Clear();

            IList<IList<Object>> results;
            IList<double> partialDistances = new List<double>();
            foreach (double distance in Settings.Distances.Keys)
            {
                //Scale down the distances, so we get the high scores
                partialDistances.Add(distance * Settings.PercentOfDistance / 100.0);
            }
            progressBar.Visible = true;
            progressBar.Minimum = 0;
            progressBar.Maximum = activities.Count;
            results = (IList<IList<Object>>)
                Settings.HighScore.GetMethod("getFastestTimesOfDistances").Invoke(null,
                new object[] { activities, partialDistances, progressBar });
            progressBar.Visible = false;

            int index = 0;
            foreach (IList<Object> result in results)
            {
                IActivity foundActivity = (IActivity)result[0];
                double old_time = double.Parse(result[1].ToString());
                double meterStart = (double)result[2];
                double meterEnd = (double)result[3];
                double timeStart = 0;
                if (result.Count > 4) { timeStart = double.Parse(result[4].ToString()); }
                double old_dist = meterEnd - meterStart;
                double new_dist = old_dist * 100 / Settings.PercentOfDistance;
                double new_time = predict(new_dist, old_dist, old_time);
                float x = (float)UnitUtil.Distance.ConvertFrom(new_dist);
                if (!x.Equals(float.NaN) && series.Points.IndexOfKey(x) == -1)
                {
                    series.Points.Add(x, new PointF(x, (float)new_time));
                }

                //length is the distance HighScore tried to get a prediction  for, may differ to actual dist
                double length = Settings.Distances.Keys[index];
                DataRow row = set.NewRow();
                row[0] = UnitUtil.Distance.ToString(new_dist);
                if (Settings.Distances[length].Values[0])
                {
                    row[1] = UnitUtil.Distance.ToString(length, "u");
                }
                else
                {
                    row[1] = UnitUtil.Distance.ToString(length, Settings.Distances[length].Keys[0], "u");
                }
                row[2] = UnitUtil.Time.ToString(new_time);
                double speed = new_dist / new_time;
                row[3] = UnitUtil.PaceOrSpeed.ToString(Settings.ShowPace, speed);
                row[4] = foundActivity.StartTime.ToLocalTime().ToShortDateString();
                row[5] = foundActivity.StartTime.AddSeconds(timeStart).ToLocalTime().ToShortTimeString();
                row[6] = UnitUtil.Time.ToString(old_time);
                row[7] = UnitUtil.Distance.ToString(meterStart);
                row[8] = UnitUtil.Distance.ToString(old_dist);
                row[ActivityIdColumn] = foundActivity.ReferenceId;
                set.Rows.Add(row);
                index++;
            }
            for (int i = index; i < Settings.Distances.Count; i++)
            {
                DataRow row = set.NewRow();
                double key = Settings.Distances.Keys[i];
                Length.Units unit = Settings.Distances[key].Keys[0];
                row[0] = UnitUtil.Distance.ToString(key);
                if (Settings.Distances[key][unit])
                {
                    row[1] = UnitUtil.Distance.ToString(key, "u");
                }
                else
                {
                    row[1] = UnitUtil.Distance.ToString(key, unit, "u");
                }

                row[4] = Resources.NoSeedActivity;
                set.Rows.Add(row);
            }
        }

        private void makeData(DataTable set, ChartDataSeries series, 
            PredictTime predict, double old_dist, double old_time)
        {
            set.Clear();
            set.Columns.Clear();
            if (null != set || null != set.Columns)
            {
                set.Columns.Add(UnitUtil.Distance.LabelAxis);
                set.Columns.Add(CommonResources.Text.LabelDistance);
                set.Columns.Add(Resources.PredictedTime, typeof(TimeSpan));
                if (Settings.ShowPace)
                {
                    set.Columns.Add(UnitUtil.Pace.LabelAxis);
                }
                else
                {
                    set.Columns.Add(UnitUtil.Speed.LabelAxis, typeof(double));
                }

                series.Points.Clear();

                foreach (double new_dist in Settings.Distances.Keys)
                {
                    double new_time = predict(new_dist, old_dist, old_time);
                    float x = (float)UnitUtil.Distance.ConvertFrom(new_dist);
                    if (!x.Equals(float.NaN) && series.Points.IndexOfKey(x) == -1)
                    {
                        series.Points.Add(x, new PointF(x, (float)new_time));
                    }

                    double length = new_dist;
                    DataRow row = set.NewRow();
                    row[0] = UnitUtil.Distance.ToString(length);
                    if (Settings.Distances[new_dist].Values[0])
                    {
                        row[1] = UnitUtil.Distance.ToString(length, "u");
                    }
                    else
                    {
                        row[1] = UnitUtil.Distance.ToString(length, Settings.Distances[new_dist].Keys[0], "u");
                    }
                    row[2] = UnitUtil.Time.ToString(new_time);
                    double speed = new_dist / new_time;
                    row[3] = UnitUtil.PaceOrSpeed.ToString(Settings.ShowPace, speed);
                    set.Rows.Add(row);
                }
            }
        }

        public void updateChartVisibility()
        {
            if (_showPage && Settings.ShowPrediction && activities.Count > 0)
            {
                if (Settings.ShowChart)
                {
                    dataGrid.Visible = false;
                    chart.Visible = true;
                }
                else
                {
                    dataGrid.Visible = true;
                    chart.Visible = false;
                }
            }
        }
        /**************************************************/

        private void selectedRow_DoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            int rowIndex = e.RowIndex;
            if (rowIndex >= 0 && dataGrid.Columns[ActivityIdColumn] != null)
            {
                object id = dataGrid.Rows[rowIndex].Cells[ActivityIdColumn].Value;
                if (id != null)
                {
                    string bookmark = "id=" + id;
                    Plugin.GetApplication().ShowView(GpsRunningPlugin.GUIDs.OpenView, bookmark);
                }
            }
        }

        //Maphandling copy&paste from Overlay/UniqueRoutes
        void dataGrid_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            int rowIndex = e.RowIndex;
            if (rowIndex >= 0 && dataGrid.Columns[ActivityIdColumn] != null)
            {
                string actid = (string)dataGrid.Rows[rowIndex].Cells[ActivityIdColumn].Value;

                IActivity id = null;
                foreach (IActivity act in activities)
                {
                    if (act.ReferenceId == actid)
                    {
                        id = act;
                    }
                }
                if (id != null)
                {
                    if (_showPage && isSingleView != true)
                    {
                        IDictionary<string, MapPolyline> routes = new Dictionary<string, MapPolyline>();
                        TrailMapPolyline m = new TrailMapPolyline(
                            new TrailResult(new ActivityWrapper(id, Plugin.GetApplication().SystemPreferences.RouteSettings.RouteColor)));
                        routes.Add(m.key, m);
                        m_layer.TrailRoutes = routes;
                    }
                    IValueRangeSeries<double> t = new ValueRangeSeries<double>();
                    t.Add(new ValueRange<double>(
                        UnitUtil.Distance.Parse((string)dataGrid.Rows[rowIndex].Cells[7].Value),
                        UnitUtil.Distance.Parse((string)dataGrid.Rows[rowIndex].Cells[7].Value) +
                        UnitUtil.Distance.Parse((string)dataGrid.Rows[rowIndex].Cells[8].Value)));
                    IList<TrailResultMarked> aTrm = new List<TrailResultMarked>();
                    aTrm.Add(new TrailResultMarked(
                        new TrailResult(new ActivityWrapper(id, Plugin.GetApplication().SystemPreferences.RouteSettings.RouteSelectedColor)),
                        t));
                    this.MarkTrack(aTrm);
                }
            }
        }

        //Some views like mapping is only working in single view - there are likely better tests
        public bool isSingleView
        {
            get
            {
#if !ST_2_1
                if (m_view != null && CollectionUtils.GetSingleItemOfType<IActivity>(m_view.SelectionProvider.SelectedItems) == null)
                {
                    return false;
                }
#endif
                return true;
            }
        }

        public void MarkTrack(IList<TrailResultMarked> atr)
        {
#if !ST_2_1
            if (_showPage)
            {
                IDictionary<string, MapPolyline> result = new Dictionary<string, MapPolyline>();
                if (m_view != null &&
                    m_view.RouteSelectionProvider != null &&
                    isSingleView == true)
                {
                    if (atr.Count > 0)
                    {
                        //Only one activity, OK to merge selections on one track
                        TrailsItemTrackSelectionInfo r = TrailResultMarked.SelInfoUnion(atr);
                        r.Activity = atr[0].trailResult.Activity;
                        m_view.RouteSelectionProvider.SelectedItems = new IItemTrackSelectionInfo[] { r };
                        m_layer.DoZoom(GPS.GetBounds(atr[0].trailResult.GpsPoints(r)));

                    }
                }
                else
                {
                    foreach (TrailResultMarked trm in atr)
                    {
                        foreach (TrailMapPolyline m in TrailMapPolyline.GetTrailMapPolyline(trm.trailResult, trm.selInfo))
                        {
                            //m.Click += new MouseEventHandler(mapPoly_Click);
                            string id = m.key;
                            result.Add(id, m);
                        }
                    }
                }
                //Update or clear
                m_layer.MarkedTrailRoutes = result;
            }
#endif
        }

        private void chkHighScore_CheckedChanged(object sender, EventArgs e)
        {
            makeData();
        }
    }
}
