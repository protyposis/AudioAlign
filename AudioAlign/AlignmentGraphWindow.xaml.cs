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
using System.Windows;
using Aurio.Matching;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace AudioAlign
{
    /// <summary>
    /// Interaction logic for AlignmentGraphWindow.xaml
    /// </summary>
    public partial class AlignmentGraphWindow : Window
    {
        private readonly List<MatchPair> matchPairs;

        public AlignmentGraphWindow(List<MatchPair> matchPairs)
        {
            InitializeComponent();

            this.matchPairs = matchPairs;
        }

        private void FillGraph(OxyPlot.PlotModel plotModel)
        {
            foreach (MatchPair matchPair in matchPairs)
            {
                AddGraphLine(plotModel, matchPair);
            }
        }

        private static void AddGraphLine(OxyPlot.PlotModel plotModel, MatchPair matchPair)
        {
            var lineSeries = new LineSeries
            {
                Title = matchPair.Track1.Name + " <-> " + matchPair.Track2.Name,
                TrackerFormatString = "{0}\n{1}: {2}\n{3}: {4}" // bugfix https://github.com/oxyplot/oxyplot/issues/265
            };
            matchPair
                .Matches
                .OrderBy(match => match.Track1Time)
                .ToList()
                .ForEach(
                    match =>
                        lineSeries
                            .Points
                            .Add(
                                new DataPoint(
                                    DateTimeAxis.ToDouble(match.Track1.Offset + match.Track1Time),
                                    DateTimeAxis.ToDouble(
                                        (match.Track1.Offset + match.Track1Time)
                                            - (match.Track2.Offset + match.Track2Time)
                                    )
                                )
                            )
                );
            plotModel.Series.Add(lineSeries);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var plotModel = new OxyPlot.PlotModel();
            var timeSpanAxis1 = new TimeSpanAxis { Title = "Time", Position = AxisPosition.Bottom };
            plotModel.Axes.Add(timeSpanAxis1);
            var timeSpanAxis2 = new MsecTimeSpanAxis
            {
                Title = "Offset",
                StringFormat = "m:ss:fff",
                MajorGridlineStyle = LineStyle.Automatic,
                MinorGridlineStyle = LineStyle.Automatic
            };
            plotModel.Axes.Add(timeSpanAxis2);
            FillGraph(plotModel);
            plotModel.IsLegendVisible = false;
            plotter.Model = plotModel;
        }

        /// <summary>
        /// Modifies OxyPlot's TimeSpanAxis calculation to display labels for smaller intervals. This
        /// is helpful on the Y-axis offset plotting to visualize offsets with higher precision. A min
        /// interval of the default 1 second is too coarse to inspect offsets on the millisecond level.
        /// https://github.com/oxyplot/oxyplot/blob/v2014.1.546/Source/OxyPlot/Axes/TimeSpanAxis.cs
        /// </summary>
        class MsecTimeSpanAxis : TimeSpanAxis
        {
            protected override double CalculateActualInterval(
                double availableSize,
                double maxIntervalSize
            )
            {
                double range = Math.Abs(ActualMinimum - ActualMaximum);
                double interval = 0.001;
                var goodIntervals = new[]
                {
                    0.001,
                    0.002,
                    0.005,
                    0.01,
                    0.02,
                    0.05,
                    0.1,
                    0.2,
                    0.5,
                    1.0,
                    5,
                    10,
                    30,
                    60,
                    120,
                    300,
                    600,
                    900,
                    1200,
                    1800,
                    3600
                };

                int maxNumberOfIntervals = Math.Max((int)(availableSize / maxIntervalSize), 2);

                while (true)
                {
                    if (range / interval < maxNumberOfIntervals)
                    {
                        return interval;
                    }

                    double nextInterval = goodIntervals.FirstOrDefault(i => i > interval);
                    if (Math.Abs(nextInterval) < double.Epsilon)
                    {
                        nextInterval = interval * 2;
                    }

                    interval = nextInterval;
                }
            }

            /// <summary>
            /// Adds missing negative signs to axis labels when a custom format is used.
            ///
            /// The axis formatter converts the `double`-parameter to a `TimeSpan` and formats
            /// it with `string.Format`. By default, when no custom format is specified, it adds
            /// the negative sign to negative time spans.
            /// When specifying a custom format, the negative sign is no longer added (only to time
            /// spans; it is still added to other types), so it needs to be additionally added
            /// to the formatted string.
            /// </summary>
            protected override string FormatValueOverride(double x)
            {
                var format = base.FormatValueOverride(x);

                if (StringFormat != null && x < 0)
                {
                    format = "-" + format;
                }

                return format;
            }
        }
    }
}
