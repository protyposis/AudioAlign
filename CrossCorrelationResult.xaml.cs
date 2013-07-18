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
using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using Microsoft.Research.DynamicDataDisplay.PointMarkers;

namespace AudioAlign {
    /// <summary>
    /// Interaction logic for CrossCorrelationResult.xaml
    /// </summary>
    public partial class CrossCorrelationResult : Window {

        public CrossCorrelationResult(CrossCorrelation.Result ccr) {
            InitializeComponent();

            PlotResult(plotter1, ccr);
            PlotResult(plotter2, ccr.AbsoluteResult());

            plotter1.Viewport.SetBinding(Viewport2D.VisibleProperty,
                                         new Binding("Visible") {Source = plotter2.Viewport, Mode = BindingMode.TwoWay});
        }

        private void PlotResult(ChartPlotter plotter, CrossCorrelation.Result ccr) {
            int[] xValues = new int[ccr.Correlations.Length];
            for (int i = 0; i < xValues.Length; i++) {
                xValues[i] = i;
            }

            var dsx = new EnumerableDataSource<int>(xValues);
            var dsy = new EnumerableDataSource<float>(ccr.Correlations);
            var ds = new CompositeDataSource(dsx, dsy);
            dsx.SetXMapping(x => x);
            dsy.SetYMapping(y => y);
            plotter.AddLineGraph(ds, Colors.CornflowerBlue, 1d, "Correlation");

            var dsmax = new RawDataSource(new Point(0, ccr.MaxValue), new Point(xValues.Length - 1, ccr.MaxValue));
            plotter.AddLineGraph(dsmax, Colors.Green, 1d, "Max");

            var dsmaxmarker = new RawDataSource(new Point(ccr.MaxIndex, ccr.MaxValue));
            plotter.AddLineGraph(dsmaxmarker, new Pen(Brushes.Magenta, 1d), new CirclePointMarker(), new PenDescription("Max"));

            var avg = ccr.Correlations.Average();
            var dsavg = new RawDataSource(new Point(0, avg), new Point(xValues.Length - 1, avg));
            plotter.AddLineGraph(dsavg, Colors.Red, 1d, "Avg");

            var absavg = ccr.Correlations.Average(f => Math.Abs(f));
            var dsabsavg = new RawDataSource(new Point(0, absavg), new Point(xValues.Length - 1, absavg));
            plotter.AddLineGraph(dsabsavg, Colors.Orange, 1d, "Abs Avg");

            plotter.LegendVisible = false;
            plotter.Viewport.Visible = new Rect(0, -1, xValues.Length, 2);
        }
    }
}
