//
// AudioAlign: Audio Synchronization and Analysis Tool
// Copyright (C) 2010-2015  Mario Guggenberger <mg@protyposis.net>
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Aurio.Project;
using Aurio.TaskMonitor;
using Aurio.Matching;
using Aurio;
using System.Collections.ObjectModel;
using System.Data;
using AudioAlign.UI;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace AudioAlign
{
    /// <summary>
    /// Interaction logic for AnalysisWindow.xaml
    /// </summary>
    public partial class AnalysisWindow : Window
    {
        private ProgressMonitor progressMonitor;
        private TrackList<AudioTrack> trackList;
        private DataTable dataTable;

        public AnalysisMode AnalysisMode { get; set; }
        public TimeSpan AnalysisWindowSize { get; set; }
        public TimeSpan AnalysisIntervalLength { get; set; }
        public int AnalysisSampleRate { get; set; }

        public AnalysisWindow(TrackList<AudioTrack> trackList)
        {
            // init data bound variables (since they're not dependency properties, they will only be read once
            // when the control with the applied binding initializes, so the variabled need to be initialized
            // before InitializeComponent() is called)
            AnalysisMode = AnalysisMode.Correlation;
            AnalysisWindowSize = new TimeSpan(0, 0, 1);
            AnalysisIntervalLength = new TimeSpan(0, 0, 30);
            AnalysisSampleRate = 22050;

            InitializeComponent();

            progressMonitor = new ProgressMonitor();
            this.trackList = trackList;

            dataTable = new DataTable();
            dataTable.Columns.Add("Time", typeof(TimeSpan));
            dataTable.Columns.Add("Min", typeof(double));
            dataTable.Columns.Add("Max", typeof(double));
            dataTable.Columns.Add("Σ+", typeof(double));
            dataTable.Columns.Add("Σ-", typeof(double));
            dataTable.Columns.Add("|Σ|", typeof(double));
            dataTable.Columns.Add("μ+", typeof(double));
            dataTable.Columns.Add("μ-", typeof(double));
            dataTable.Columns.Add("|μ|", typeof(double));
            dataTable.Columns.Add("%", typeof(double));

            var graphStyles = new Tuple<OxyColor, MarkerType>[]
            {
                new Tuple<OxyColor, MarkerType>(OxyColors.YellowGreen, MarkerType.Cross), // down
                new Tuple<OxyColor, MarkerType>(OxyColors.YellowGreen, MarkerType.Plus), // up
                new Tuple<OxyColor, MarkerType>(OxyColors.Magenta, MarkerType.Plus), // up
                new Tuple<OxyColor, MarkerType>(OxyColors.Magenta, MarkerType.Cross), // down
                new Tuple<OxyColor, MarkerType>(OxyColors.Magenta, MarkerType.Circle),
                new Tuple<OxyColor, MarkerType>(OxyColors.Cyan, MarkerType.Plus), // up
                new Tuple<OxyColor, MarkerType>(OxyColors.Cyan, MarkerType.Cross), // down
                new Tuple<OxyColor, MarkerType>(OxyColors.Cyan, MarkerType.Circle),
                new Tuple<OxyColor, MarkerType>(OxyColors.Red, MarkerType.Circle)
            };

            var plotModel = new PlotModel();

            // setup plotter axes and viewport
            plotModel.Axes.Add(
                new TimeSpanAxis()
                {
                    Position = AxisPosition.Bottom,
                    Minimum = 0,
                    Maximum = TimeSpanAxis.ToDouble(trackList.End - trackList.Start)
                }
            );
            plotModel.Axes.Add(
                new LinearAxis()
                {
                    Minimum = -0.1,
                    Maximum = 1.2,
                    MajorGridlineStyle = LineStyle.Automatic,
                    MinorGridlineStyle = LineStyle.Automatic
                }
            );

            // setup plotter graph lines
            for (int i = 1; i < dataTable.Columns.Count; i++)
            {
                DataColumn column = dataTable.Columns[i];
                plotModel.Series.Add(
                    new LineSeries
                    {
                        Color = graphStyles[i - 1].Item1,
                        MarkerType = graphStyles[i - 1].Item2,
                        MarkerFill = OxyColors.Red,
                        MarkerStroke = OxyColors.Red,
                        Title = column.ColumnName,
                        TrackerFormatString = "{0}\nTime: {2}\nValue: {4}" // bugfix https://github.com/oxyplot/oxyplot/issues/265
                    }
                );
            }

            plotModel.IsLegendVisible = false;
            resultPlotter.Model = plotModel;

            dataTable.RowChanged += delegate(object sender, DataRowChangeEventArgs e)
            {
                if (e.Action == DataRowAction.Add)
                {
                    TimeSpan time = (TimeSpan)e.Row.ItemArray[0];
                    for (int x = 0; x < plotModel.Series.Count; x++)
                    {
                        ((LineSeries)plotModel.Series[x]).Points.Add(
                            new DataPoint(
                                TimeSpanAxis.ToDouble(time),
                                (double)e.Row.ItemArray[x + 1]
                            )
                        );
                    }
                }
                plotModel.InvalidatePlot(false);
            };
            dataTable.TableCleared += delegate(object sender, DataTableClearEventArgs e)
            {
                for (int x = 0; x < plotModel.Series.Count; x++)
                {
                    ((LineSeries)plotModel.Series[x]).Points.Clear();
                }
                plotModel.InvalidatePlot(false);
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            NonClientRegionAPI.Glassify(this);

            progressBar.IsEnabled = false;
            progressMonitor.ProcessingStarted += Instance_ProcessingStarted;
            progressMonitor.ProcessingProgressChanged += Instance_ProcessingProgressChanged;
            progressMonitor.ProcessingFinished += Instance_ProcessingFinished;
            ProgressMonitor.GlobalInstance.AddChild(progressMonitor);
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            ProgressMonitor.GlobalInstance.RemoveChild(progressMonitor);
            progressMonitor.ProcessingStarted -= Instance_ProcessingStarted;
            progressMonitor.ProcessingProgressChanged -= Instance_ProcessingProgressChanged;
            progressMonitor.ProcessingFinished -= Instance_ProcessingFinished;
        }

        private void Instance_ProcessingStarted(object sender, EventArgs e)
        {
            progressBar.Dispatcher.BeginInvoke(
                (Action)
                    delegate
                    {
                        parameterGroupBox.IsEnabled = false;
                        progressBar.IsEnabled = true;
                        progressBarLabel.Text = progressMonitor.StatusMessage;
                    }
            );
        }

        private void Instance_ProcessingProgressChanged(object sender, ValueEventArgs<float> e)
        {
            progressBar.Dispatcher.BeginInvoke(
                (Action)
                    delegate
                    {
                        progressBar.Value = e.Value;
                        progressBarLabel.Text = progressMonitor.StatusMessage;
                    }
            );
        }

        private void Instance_ProcessingFinished(object sender, EventArgs e)
        {
            progressBar.Dispatcher.BeginInvoke(
                (Action)
                    delegate
                    {
                        progressBar.Value = 0;
                        progressBar.IsEnabled = false;
                        progressBarLabel.Text = "";
                        parameterGroupBox.IsEnabled = true;
                    }
            );
        }

        private void analyzeButton_Click(object sender, RoutedEventArgs e)
        {
            Analysis analysis = new Analysis(
                AnalysisMode,
                trackList,
                AnalysisWindowSize,
                AnalysisIntervalLength,
                AnalysisSampleRate,
                progressMonitor
            );

            ObservableCollection<AnalysisEventArgs> windowResults =
                new ObservableCollection<AnalysisEventArgs>();
            analysisResultsGrid.ItemsSource = windowResults;

            dataTable.Clear();

            analysis.WindowAnalysed += new EventHandler<AnalysisEventArgs>(
                delegate(object sender2, AnalysisEventArgs e2)
                {
                    analysisResultsGrid.Dispatcher.Invoke(
                        (Action)
                            delegate
                            {
                                windowResults.Add(e2);
                                dataTable.Rows.Add(
                                    e2.Time,
                                    e2.Min,
                                    e2.Max,
                                    e2.SumPositive,
                                    e2.SumNegative,
                                    e2.SumAbsolute,
                                    e2.AveragePositive,
                                    e2.AverageNegative,
                                    e2.AverageAbsolute,
                                    e2.Score
                                );
                            }
                    );
                }
            );
            analysis.Finished += new EventHandler<AnalysisEventArgs>(
                delegate(object sender2, AnalysisEventArgs e2)
                {
                    analysisResultsGrid.Dispatcher.Invoke(
                        (Action)
                            delegate
                            {
                                windowResults.Add(e2);
                            }
                    );
                }
            );
            analysis.ExecuteAsync();
        }
    }
}
