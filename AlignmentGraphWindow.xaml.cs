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
            var timeSpanAxis2 = new TimeSpanAxis();
            timeSpanAxis2.Title = "Offset";
            plotModel.Axes.Add(timeSpanAxis2);
            FillGraph(plotModel);
            plotModel.IsLegendVisible = false;
            plotter.Model = plotModel;
        }

        private void MenuItemCopyToClipboard_Click(object sender, RoutedEventArgs e) {
            Clipboard.SetImage(OxyPlot.Wpf.PngExporter.ExportToBitmap(plotter.Model, (int)plotter.ActualWidth, (int)plotter.ActualHeight, OxyColors.White));
        }
    }
}
