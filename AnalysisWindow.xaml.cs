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
using AudioAlign.Audio.Project;
using AudioAlign.Audio.TaskMonitor;
using AudioAlign.Audio.Matching;
using AudioAlign.Audio;
using System.Collections.ObjectModel;
using System.Data;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using Microsoft.Research.DynamicDataDisplay.PointMarkers;
using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.Charts;

namespace AudioAlign {
    /// <summary>
    /// Interaction logic for AnalysisWindow.xaml
    /// </summary>
    public partial class AnalysisWindow : Window {

        private ProgressMonitor progressMonitor;
        private TrackList<AudioTrack> trackList;
        private DataTable dataTable;

        public AnalysisMode AnalysisMode { get; set; }
        public int AnalysisWindowSize { get; set; }
        public int AnalysisIntervalLength { get; set; }
        public int AnalysisSampleRate { get; set; }

        public AnalysisWindow(TrackList<AudioTrack> trackList) {
            // init data bound variables (since they're not dependency properties, they will only be read once
            // when the control with the applied binding initializes, so the variabled need to be initialized
            // before InitializeComponent() is called)
            AnalysisMode = AnalysisMode.Correlation;
            AnalysisWindowSize = 1;
            AnalysisIntervalLength = 30;
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

            var graphStyles = new Tuple<Pen, PointMarker>[] {
                new Tuple<Pen, PointMarker>(new Pen(Brushes.YellowGreen, 1d), new Chart.TrianglePointMarker(Chart.TrianglePointMarker.Direction.Down)),
                new Tuple<Pen, PointMarker>(new Pen(Brushes.YellowGreen, 1d), new Chart.TrianglePointMarker(Chart.TrianglePointMarker.Direction.Up)),
                new Tuple<Pen, PointMarker>(new Pen(Brushes.Magenta, 1d), new Chart.TrianglePointMarker(Chart.TrianglePointMarker.Direction.Up)),
                new Tuple<Pen, PointMarker>(new Pen(Brushes.Magenta, 1d), new Chart.TrianglePointMarker(Chart.TrianglePointMarker.Direction.Down)),
                new Tuple<Pen, PointMarker>(new Pen(Brushes.Magenta, 1d), new CirclePointMarker()),
                new Tuple<Pen, PointMarker>(new Pen(Brushes.Cyan, 1d), new Chart.TrianglePointMarker(Chart.TrianglePointMarker.Direction.Up)),
                new Tuple<Pen, PointMarker>(new Pen(Brushes.Cyan, 1d), new Chart.TrianglePointMarker(Chart.TrianglePointMarker.Direction.Down)),
                new Tuple<Pen, PointMarker>(new Pen(Brushes.Cyan, 1d), new CirclePointMarker()),
                new Tuple<Pen, PointMarker>(new Pen(Brushes.Red, 2d), new CirclePointMarker())
            };

            // setup plotter axes
            HorizontalTimeSpanAxis timeSpanAxis = new HorizontalTimeSpanAxis();
            resultPlotter.HorizontalAxis = timeSpanAxis;
            
            // setup plotter viewport
            resultPlotter.Viewport.Visible = new Rect(0, -1.1, timeSpanAxis.ConvertToDouble(trackList.End - trackList.Start), 2.2);

            // setup plotter graph lines
            for (int i = 1; i < dataTable.Columns.Count; i++) {
                DataColumn column = dataTable.Columns[i];
                TableDataSource dataSource = new TableDataSource(dataTable);
                dataSource.SetXMapping(row => timeSpanAxis.ConvertToDouble((TimeSpan)row[0]));
                dataSource.SetYMapping(row => (double)row[column]);
                resultPlotter.AddLineGraph(dataSource, 
                    graphStyles[i - 1].Item1,
                    graphStyles[i - 1].Item2,
                    new PenDescription(column.ColumnName));
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            NonClientRegionAPI.Glassify(this);

            progressBar.IsEnabled = false;
            progressMonitor.ProcessingStarted += Instance_ProcessingStarted;
            progressMonitor.ProcessingProgressChanged += Instance_ProcessingProgressChanged;
            progressMonitor.ProcessingFinished += Instance_ProcessingFinished;
            ProgressMonitor.GlobalInstance.AddChild(progressMonitor);
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e) {
            ProgressMonitor.GlobalInstance.RemoveChild(progressMonitor);
            progressMonitor.ProcessingStarted -= Instance_ProcessingStarted;
            progressMonitor.ProcessingProgressChanged -= Instance_ProcessingProgressChanged;
            progressMonitor.ProcessingFinished -= Instance_ProcessingFinished;
        }

        private void Instance_ProcessingStarted(object sender, EventArgs e) {
            progressBar.Dispatcher.BeginInvoke((Action)delegate {
                parameterGroupBox.IsEnabled = false;
                progressBar.IsEnabled = true;
                progressBarLabel.Text = progressMonitor.StatusMessage;
            });
        }

        private void Instance_ProcessingProgressChanged(object sender, ValueEventArgs<float> e) {
            progressBar.Dispatcher.BeginInvoke((Action)delegate {
                progressBar.Value = e.Value;
                progressBarLabel.Text = progressMonitor.StatusMessage;
            });
        }

        private void Instance_ProcessingFinished(object sender, EventArgs e) {
            progressBar.Dispatcher.BeginInvoke((Action)delegate {
                progressBar.Value = 0;
                progressBar.IsEnabled = false;
                progressBarLabel.Text = "";
                parameterGroupBox.IsEnabled = true;
            });
        }

        private void analyzeButton_Click(object sender, RoutedEventArgs e) {
            Analysis analysis = new Analysis(
                AnalysisMode,
                trackList, 
                new TimeSpan(0, 0, AnalysisWindowSize),
                new TimeSpan(0, 0, AnalysisIntervalLength), 
                AnalysisSampleRate, 
                progressMonitor);
            
            ObservableCollection<AnalysisEventArgs> windowResults = new ObservableCollection<AnalysisEventArgs>();
            analysisResultsGrid.ItemsSource = windowResults;

            dataTable.Clear();

            //analysis.Started += new EventHandler(delegate(object sender2, EventArgs e2) {
            //    resultPlotter.Dispatcher.Invoke((Action)delegate {
            //    });
            //});
            analysis.WindowAnalysed += new EventHandler<AnalysisEventArgs>(delegate(object sender2, AnalysisEventArgs e2) {
                analysisResultsGrid.Dispatcher.Invoke((Action)delegate {
                    windowResults.Add(e2);
                    dataTable.Rows.Add(e2.Time, 
                        e2.Min, e2.Max, 
                        e2.SumPositive, e2.SumNegative, e2.SumAbsolute, 
                        e2.AveragePositive, e2.AverageNegative, e2.AverageAbsolute,
                        e2.Score);
                });
            });
            analysis.Finished += new EventHandler<AnalysisEventArgs>(delegate(object sender2, AnalysisEventArgs e2) {
                analysisResultsGrid.Dispatcher.Invoke((Action)delegate {
                    windowResults.Add(e2);
                });
                //resultPlotter.Dispatcher.Invoke((Action)delegate {
                //    resultPlotter.Viewport.FitToView();
                //});
            });
            analysis.ExecuteAsync();
        }
    }
}
