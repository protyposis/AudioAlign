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
using Aurio.Matching;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace AudioAlign
{
    /// <summary>
    /// Interaction logic for CrossCorrelationResult.xaml
    /// </summary>
    public partial class CrossCorrelationResult : Window
    {
        public CrossCorrelationResult(CrossCorrelation.Result ccr)
        {
            InitializeComponent();

            PlotResult(plotter1, ccr);
            PlotResult(plotter2, ccr.AbsoluteResult());

            // Sync horizontal axes

            var p1a1 = plotter1.ActualModel.Axes[0];
            var p2a1 = plotter2.ActualModel.Axes[0];

            bool isInternalChange = false;
            p1a1.AxisChanged += (s, e) =>
            {
                if (isInternalChange)
                {
                    return;
                }

                isInternalChange = true;
                p2a1.Zoom(p1a1.ActualMinimum, p1a1.ActualMaximum);
                plotter2.ActualModel.InvalidatePlot(false);
                isInternalChange = false;
            };

            p2a1.AxisChanged += (s, e) =>
            {
                if (isInternalChange)
                {
                    return;
                }

                isInternalChange = true;
                p1a1.Zoom(p2a1.ActualMinimum, p2a1.ActualMaximum);
                plotter1.ActualModel.InvalidatePlot(false);
                isInternalChange = false;
            };
        }

        private void PlotResult(OxyPlot.Wpf.PlotView plotter, CrossCorrelation.Result ccr)
        {
            int[] xValues = new int[ccr.Correlations.Length];
            List<DataPoint> values = new List<DataPoint>();
            for (int i = 0; i < ccr.Correlations.Length; i++)
            {
                xValues[i] = i;
                values.Add(new DataPoint(i, ccr.Correlations[i]));
            }

            var plotModel = new PlotModel();

            // setup plotter axes and viewport
            plotModel
                .Axes
                .Add(
                    new LinearAxis()
                    {
                        Position = AxisPosition.Bottom,
                        Minimum = 0,
                        Maximum = xValues.Length
                    }
                );
            plotModel
                .Axes
                .Add(
                    new LinearAxis()
                    {
                        Minimum = -1,
                        Maximum = 2,
                        MajorGridlineStyle = LineStyle.Automatic,
                        MinorGridlineStyle = LineStyle.Automatic
                    }
                );

            // correlation series
            plotModel
                .Series
                .Add(
                    new LineSeries
                    {
                        ItemsSource = values,
                        Color = OxyColors.CornflowerBlue,
                        Title = "Correlation"
                    }
                );

            // distinct values
            plotModel
                .Annotations
                .Add(
                    new LineAnnotation
                    {
                        Type = LineAnnotationType.Horizontal,
                        Y = ccr.MaxValue,
                        Color = OxyColors.Green,
                        ClipByXAxis = false
                    }
                );

            plotModel
                .Annotations
                .Add(
                    new PointAnnotation
                    {
                        X = ccr.MaxIndex,
                        Y = ccr.MaxValue,
                        Fill = OxyColors.Red,
                        Text = "Max"
                    }
                );

            var avg = ccr.Correlations.Average();
            plotModel
                .Annotations
                .Add(
                    new LineAnnotation
                    {
                        Type = LineAnnotationType.Horizontal,
                        Y = avg,
                        Color = OxyColors.Red,
                        ClipByXAxis = false,
                        Text = "Avg"
                    }
                );

            var absavg = ccr.Correlations.Average(f => Math.Abs(f));
            plotModel
                .Annotations
                .Add(
                    new LineAnnotation
                    {
                        Type = LineAnnotationType.Horizontal,
                        Y = absavg,
                        Color = OxyColors.Orange,
                        ClipByXAxis = false,
                        Text = "Abs Avg"
                    }
                );

            plotModel.IsLegendVisible = false;
            plotter.Model = plotModel;
        }
    }
}
