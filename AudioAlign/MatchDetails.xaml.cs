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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Aurio;
using Aurio.Matching;
using Aurio.Project;
using Aurio.Streams;
using Aurio.TaskMonitor;

namespace AudioAlign {
    /// <summary>
    /// Interaction logic for MatchDetails.xaml
    /// </summary>
    public partial class MatchDetails : Window {

        private Match match;
        private TrackList<AudioTrack> trackList;
        private MultitrackPlayer player;
        private CrossCorrelation.Result ccr;
        private ProgressMonitor progressMonitor;

        public MatchDetails(Match match) {
            InitializeComponent();

            this.match = match;

            trackList = new TrackList<AudioTrack> {match.Track1, match.Track2};

            progressMonitor = new ProgressMonitor();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            // INIT COMMAND BINDINGS
            CommandBinding playBinding = new CommandBinding(MediaCommands.Play);
            CommandBindings.Add(playBinding);
            playBinding.CanExecute += playCommandBinding_CanExecute;
            playBinding.Executed += playCommandBinding_Executed;

            CommandBinding pauseBinding = new CommandBinding(MediaCommands.Pause);
            CommandBindings.Add(pauseBinding);
            pauseBinding.CanExecute += pauseCommandBinding_CanExecute;
            pauseBinding.Executed += pauseCommandBinding_Executed;

            CommandBinding playToggleBinding = new CommandBinding(Commands.PlayToggle);
            CommandBindings.Add(playToggleBinding);
            playToggleBinding.Executed += playToggleBinding_Executed;

            // Execute the following code after window and controls are fully loaded and initialized
            // http://stackoverflow.com/a/1746975
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)(() => {
                multiTrackViewer1.ItemsSource = trackList;
                multiTrackViewer1.Matches.Add(match);
                multiTrackViewer1.SelectedMatch = match;
                this.Focus();
            }));
            // the following must be called separately on the dispatcher, else the track controls are not initialized yet
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)ZoomToMatch);


            // INIT PLAYER
            player = new MultitrackPlayer(trackList);
            player.VolumeAnnounced += (sender2, e2) => Dispatcher.BeginInvoke((Action) (() => {
                if (e2.MaxSampleValues.Length >= 2) {
                    stereoVUMeter1.AmplitudeLeft = e2.MaxSampleValues[0];
                    stereoVUMeter1.AmplitudeRight = e2.MaxSampleValues[1];
                }
            }));

            player.CurrentTimeChanged += (sender2, e2) => multiTrackViewer1.Dispatcher.BeginInvoke((Action) (() => {
                multiTrackViewer1.VirtualCaretOffset = e2.Value.Ticks;
                // autoscroll
                if (multiTrackViewer1.VirtualViewportInterval.To <= multiTrackViewer1.VirtualCaretOffset) {
                    multiTrackViewer1.VirtualViewportOffset = multiTrackViewer1.VirtualCaretOffset;
                }
            }));

            player.PlaybackStateChanged += (sender2, e2) =>
                multiTrackViewer1.Dispatcher.BeginInvoke((Action) CommandManager.InvalidateRequerySuggested);

            volumeSlider.ValueChanged += (sender2, e2) => player.Volume = (float) e2.NewValue;

            // INIT PROGRESSBAR
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

        private void Window_Closed(object sender, EventArgs e) {
            player.Dispose();
        }

        private void btnViewMatch_Click(object sender, RoutedEventArgs e) {
            ZoomToMatch();
        }

        private void playCommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = player.CanPlay;
            e.Handled = true;
        }

        private void pauseCommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = player.CanPause;
            e.Handled = true;
        }

        private void playCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e) {
            player.CurrentTime = new TimeSpan(multiTrackViewer1.VirtualCaretOffset);
            player.Play();
        }

        private void pauseCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e) {
            player.Pause();
        }

        private void playToggleBinding_Executed(object sender, ExecutedRoutedEventArgs e) {
            if (player.CanPlay) {
                playCommandBinding_Executed(sender, e);
            }
            else if (player.CanPause) {
                pauseCommandBinding_Executed(sender, e);
            }
        }

        public void ZoomToMatch() {
            TimeSpan t1 = match.Track1.Offset + match.Track1Time;
            TimeSpan t2 = match.Track2.Offset + match.Track2Time;
            TimeSpan diff = t1 - t2;
            TimeSpan matchPosition = t1 - new TimeSpan(diff.Ticks / 2);
            multiTrackViewer1.VirtualViewportWidth = new TimeSpan(0, 0, 5).Ticks;
            multiTrackViewer1.Display(matchPosition, true);
            multiTrackViewer1.FitTracksVertically(50);
        }

        private void crossCorrelateButton_Click(object sender, RoutedEventArgs e) {
            if (ccr == null) {
                Task.Factory.StartNew(() => {
                    Match ccm = CrossCorrelation.Adjust(match, progressMonitor, out ccr);
                    Dispatcher.BeginInvoke((Action) delegate {
                        multiTrackViewer1.Matches.Add(ccm);
                        multiTrackViewer1.RefreshAdornerLayer();

                        ShowCCResult(ccr);
                    });
                });
            } else {
                ShowCCResult(ccr);
            }
        }

        private void syncButton_Click(object sender, RoutedEventArgs e) {
            MatchProcessor.Align(match);
        }

        private void syncCCButton_Click(object sender, RoutedEventArgs e) {
            if (multiTrackViewer1.Matches.Count > 1) {
                MatchProcessor.Align(multiTrackViewer1.Matches[1]);
            }
        }

        private void ShowCCResult(CrossCorrelation.Result ccr) {
            new CrossCorrelationResult(ccr) {
                Owner = this
            }.Show();
        }

        private void Instance_ProcessingStarted(object sender, EventArgs e) {
            progressBar.Dispatcher.BeginInvoke((Action)(() => progressBar.IsEnabled = true));
        }

        private void Instance_ProcessingProgressChanged(object sender, ValueEventArgs<float> e) {
            progressBar.Dispatcher.BeginInvoke((Action)(() => progressBar.Value = e.Value));
        }

        private void Instance_ProcessingFinished(object sender, EventArgs e) {
            progressBar.Dispatcher.BeginInvoke((Action)(() => {
                progressBar.Value = 0;
                progressBar.IsEnabled = false;
            }));
        }
    }
}
