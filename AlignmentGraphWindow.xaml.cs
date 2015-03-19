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
using AudioAlign.Audio.Matching;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot;
using System.IO;

namespace AudioAlign {
    /// <summary>
    /// Interaction logic for AlignmentGraphWindow.xaml
    /// </summary>
    public partial class AlignmentGraphWindow : Window {

        private List<MatchPair> matchPairs;

        public AlignmentGraphWindow(List<MatchPair> matchPairs) {
            InitializeComponent();

            this.matchPairs = matchPairs;
        }

        private void FillGraph(OxyPlot.PlotModel plotModel) {
            foreach (MatchPair matchPair in matchPairs) {
                AddGraphLine(plotModel, matchPair);
            }
        }

        private void AddGraphLine(OxyPlot.PlotModel plotModel, MatchPair matchPair) {
            var lineSeries = new LineSeries();
            lineSeries.Title = matchPair.Track1.Name + " <-> " + matchPair.Track2.Name;
            lineSeries.TrackerFormatString = "{0}\n{1}: {2}\n{3}: {4}"; // bugfix https://github.com/oxyplot/oxyplot/issues/265
            matchPair.Matches.OrderBy(match => match.Track1Time).ToList()
                .ForEach(match => lineSeries.Points.Add(new DataPoint(
                    DateTimeAxis.ToDouble(match.Track1.Offset + match.Track1Time), 
                    DateTimeAxis.ToDouble((match.Track1.Offset + match.Track1Time) - (match.Track2.Offset + match.Track2Time)))));
            plotModel.Series.Add(lineSeries);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            var plotModel = new OxyPlot.PlotModel();
            var timeSpanAxis1 = new TimeSpanAxis();
            timeSpanAxis1.Title = "Time";
            timeSpanAxis1.Position = AxisPosition.Bottom;
            plotModel.Axes.Add(timeSpanAxis1);
            var timeSpanAxis2 = new MsecTimeSpanAxis();
            timeSpanAxis2.Title = "Offset";
            timeSpanAxis2.StringFormat = "m:ss:msec";
            plotModel.Axes.Add(timeSpanAxis2);
            FillGraph(plotModel);
            plotModel.IsLegendVisible = false;
            plotter.Model = plotModel;
        }

        private void MenuItemCopyToClipboard_Click(object sender, RoutedEventArgs e) {
            Clipboard.SetImage(OxyPlot.Wpf.PngExporter.ExportToBitmap(plotter.Model, (int)plotter.ActualWidth, (int)plotter.ActualHeight, OxyColors.White));
        }

        /// <summary>
        /// Modifies OxyPlot's TimeSpanAxis calculation to display labels for smaller intervals. This
        /// is helpful on the Y-axis offset plotting to visualize offsets with higher precision. A min
        /// interval of the default 1 second is to coarse to inspect offsets on the millisecond level.
        /// https://github.com/oxyplot/oxyplot/blob/v2014.1.546/Source/OxyPlot/Axes/TimeSpanAxis.cs
        /// </summary>
        class MsecTimeSpanAxis : TimeSpanAxis {
            protected override double CalculateActualInterval(double availableSize, double maxIntervalSize) {
                double range = Math.Abs(this.ActualMinimum - this.ActualMaximum);
                double interval = 0.001;
                var goodIntervals = new[] { 0.001, 0.002, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1.0, 5, 10, 30, 60, 120, 300, 600, 900, 1200, 1800, 3600 };

                int maxNumberOfIntervals = Math.Max((int)(availableSize / maxIntervalSize), 2);

                while (true) {
                    if (range / interval < maxNumberOfIntervals) {
                        return interval;
                    }

                    double nextInterval = goodIntervals.FirstOrDefault(i => i > interval);
                    if (Math.Abs(nextInterval) < double.Epsilon) {
                        nextInterval = interval * 2;
                    }

                    interval = nextInterval;
                }
            }
        }
    }
}
