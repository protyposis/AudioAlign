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
using Microsoft.Research.DynamicDataDisplay.DataSources;
using Microsoft.Research.DynamicDataDisplay.Charts;
using Microsoft.Research.DynamicDataDisplay.PointMarkers;
using Microsoft.Research.DynamicDataDisplay;

namespace AudioAlign {
    /// <summary>
    /// Interaction logic for AlignmentGraphWindow.xaml
    /// </summary>
    public partial class AlignmentGraphWindow : Window {

        private List<MatchGroup> matchGroups;

        private HorizontalTimeSpanAxis horizontalAxis;
        private VerticalTimeSpanAxis verticalAxis;

        public AlignmentGraphWindow(List<MatchGroup> matchGroups) {
            InitializeComponent();

            this.matchGroups = matchGroups;
        }

        private void FillGraph() {
            foreach (MatchGroup matchGroup in matchGroups) {
                foreach (MatchPair matchPair in matchGroup.MatchPairs) {
                    AddGraphLine(matchPair);
                }
            }
        }

        private void AddGraphLine(MatchPair matchPair) {
            // matches must be sequentially ordered on the X-axis for the graph line to be drawn correctly
            EnumerableDataSource<Match> dataSource = new EnumerableDataSource<Match>(matchPair.Matches.OrderBy(match => match.Track1Time));
            dataSource.SetXMapping(match => horizontalAxis.ConvertToDouble(match.Track1.Offset + match.Track1Time));
            dataSource.SetYMapping(match => verticalAxis.ConvertToDouble((match.Track1.Offset + match.Track1Time) - (match.Track2.Offset + match.Track2Time)));
            plotter.AddLineGraph(dataSource, 2.0d);
            plotter.LegendVisible = false;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            plotter.HorizontalAxis = horizontalAxis = new HorizontalTimeSpanAxis();
            plotter.VerticalAxis = verticalAxis = new VerticalTimeSpanAxis();

            FillGraph();
        }
    }
}
