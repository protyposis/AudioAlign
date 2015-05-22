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
using System.Windows.Documents;
using System.Windows;
using System.Windows.Media;
using Aurio.Project;
using System.Windows.Controls;
using System.Diagnostics;
using Aurio.Matching;
using System.Collections.ObjectModel;
using Aurio;
using Aurio.WaveControls;

namespace AudioAlign.UI {
    class MultiTrackConnectionAdorner : Adorner {

        private MultiTrackListBox multiTrackListBox;
        private ObservableCollection<Match> matches;
        private ObservableCollection<Match> selectedMatches;
        private Color[] colors;

        public MultiTrackConnectionAdorner(UIElement adornedElement, MultiTrackListBox multiTrackListBox)
            : base(adornedElement) {
                this.multiTrackListBox = multiTrackListBox;
                matches = new ObservableCollection<Match>();
                matches.CollectionChanged += Matches_CollectionChanged;
                selectedMatches = new ObservableCollection<Match>();
            selectedMatches.CollectionChanged += Matches_CollectionChanged;

                ColorGradient gradient = new ColorGradient(0, 1);
                gradient.AddStop(Colors.DarkRed, 0);
                gradient.AddStop(Colors.Red, 0.5f);
                gradient.AddStop(Colors.Yellow, 0.65f);
                gradient.AddStop(Colors.Green, 0.8f);
                gradient.AddStop(Colors.DarkGreen, 1);
                colors = gradient.GetGradient(1024).ToArray();
        }

        public Collection<Match> Matches {
            get { return matches; }
        }

        public Match SelectedMatch {
            get {
                if(selectedMatches.Count > 0) {
                    return selectedMatches[0];
                }
                return null;
            }
            set {
                if (value != null && !matches.Contains(value)) {
                    throw new Exception("match to be selected isn't part of the match collection");
                }
                selectedMatches.Clear();
                selectedMatches.Add(value);
            }
        }

        public Collection<Match> SelectedMatches {
            get { return selectedMatches; }
        } 

        protected override void OnRender(DrawingContext drawingContext) {
            if (multiTrackListBox.ItemsSource == null || !multiTrackListBox.ItemsSource.GetEnumerator().MoveNext()) {
                return;
            }

            // NOTE the dictionary needs to be built every time because the ListBox.items.CollectionChanged event is inaccessible (protected)
            Dictionary<AudioTrack, WaveView> waveViewMappings = new Dictionary<AudioTrack, WaveView>();
            foreach (AudioTrack audioTrack in multiTrackListBox.ItemsSource) {
                ListBoxItem item = (ListBoxItem)multiTrackListBox.ItemContainerGenerator.ContainerFromItem(audioTrack);
                ContentPresenter itemContentPresenter = UIUtil.FindVisualChild<ContentPresenter>(item);
                DataTemplate itemDataTemplate = itemContentPresenter.ContentTemplate;
                WaveView waveView = (WaveView)itemDataTemplate.FindName("waveView", itemContentPresenter);
                waveViewMappings.Add(audioTrack, waveView);
            }

            Point p1, p2;
            foreach (Match match in Matches) {
                WaveView waveView1 = waveViewMappings[match.Track1];
                WaveView waveView2 = waveViewMappings[match.Track2];
                if (!CalculatePoints(match, waveViewMappings, out p1, out p2)) {
                    continue;
                }

                if (waveView1 != waveView2) {
                    Color c = colors[(int)(match.Similarity * (colors.Length - 1))];
                    drawingContext.DrawLine(new Pen(new SolidColorBrush(c), 3) {
                        DashStyle = DashStyles.Solid,
                        EndLineCap = PenLineCap.Triangle,
                        StartLineCap = PenLineCap.Triangle
                    }, p1, p2);
                }

                // draw selected match
                if(selectedMatches.Contains(match)) {
                    DrawTriangle(drawingContext, Brushes.Red, p1, p1.Y > p2.Y ? 6 : -6); // top triangle
                    DrawTriangle(drawingContext, Brushes.Red, p2, p1.Y > p2.Y ? -6 : 6); // bottom triangle
                }
            }
        }

        private void Matches_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            InvalidateVisual();
        }

        /// <summary>
        /// Calculate the points of a match that are used to draw the GUI connection lines.
        /// </summary>
        /// <param name="match"></param>
        /// <param name="waveViewMappings"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <returns>true if the match is visible and should be drawn, else false</returns>
        private bool CalculatePoints(Match match, Dictionary<AudioTrack, WaveView> waveViewMappings, out Point p1, out Point p2) {
            WaveView waveView1 = waveViewMappings[match.Track1];
            long timestamp1 = waveView1.AudioTrack.Offset.Ticks + match.Track1Time.Ticks;
            p1 = waveView1.TranslatePoint(new Point(waveView1.VirtualToPhysicalIntervalOffset(timestamp1), 0), this);

            WaveView waveView2 = waveViewMappings[match.Track2];
            long timestamp2 = waveView2.AudioTrack.Offset.Ticks + match.Track2Time.Ticks;
            p2 = waveView2.TranslatePoint(new Point(waveView2.VirtualToPhysicalIntervalOffset(timestamp2), 0), this);

            if (p1.Y < p2.Y) {
                p1.Y += waveView1.ActualHeight;
            }
            else {
                p2.Y += waveView2.ActualHeight;
            }

            // make p1 always the left point, p2 the right point
            if (p1.X > p2.X) {
                CommonUtil.Swap<Point>(ref p1, ref p2);
            }

            // find out if a match is invisible and can be skipped
            double bx1 = 0; // x-coord of left drawing boundary
            double bx2 = ActualWidth; // x-coord of right drawing boundary
            if ((p1.X >= bx1 && p1.X <= bx2)
                || (p2.X >= bx1 && p2.X <= bx2)
                || (p1.X < bx1 && p2.X > bx2)
                || (p2.X < bx1 && p1.X > bx2)) {
                // calculate bounded line drawing coordinates to avoid that lines with very long lengths need to be rendered
                // drawing of lines with lengths > 100000 is very very slow or makes the application even stop
                double k = (p2.Y - p1.Y) / (p2.X - p1.X); // line gradient
                // the following only works for cases realiably where p1 is always the left point
                if (p1.X < bx1) {
                    double delta = Math.Abs(p1.X - bx1);
                    p1.X += delta;
                    p1.Y += k * delta;
                }
                if (p2.X > bx2) {
                    double delta = Math.Abs(p2.X - bx2);
                    p2.X -= delta;
                    p2.Y -= k * delta;
                }
            }
            else {
                return false;
            }
            return true;
        }

        private static void DrawTriangle(DrawingContext drawingContext, Brush brush, Point origin, double size) {
            Point start = origin;
            LineSegment[] segments = new LineSegment[] { new LineSegment(new Point(origin.X - size, origin.Y + size), true), new LineSegment(new Point(origin.X + size, origin.Y + size), true) };
            PathFigure figure = new PathFigure(start, segments, true);
            PathGeometry geo = new PathGeometry(new PathFigure[]{figure});
            drawingContext.DrawGeometry(brush, null, geo);
        }
    }
}
