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
using AudioAlign.Audio;
using AudioAlign.Audio.Matching;
using AudioAlign.Audio.Project;
using AudioAlign.Audio.Streams;
using AudioAlign.Audio.TaskMonitor;

namespace AudioAlign {
    /// <summary>
    /// Interaction logic for MatchDetails.xaml
    /// </summary>
    public partial class MatchDetails : Window {

        private Match match;
        private TrackList<AudioTrack> trackList;
        private MultitrackPlayer player;

        public MatchDetails(Match match) {
            InitializeComponent();

            this.match = match;
            
            trackList = new TrackList<AudioTrack>();
            trackList.Add(match.Track1);
            trackList.Add(match.Track2);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            // INIT COMMAND BINDINGS
            CommandBinding playBinding = new CommandBinding(MediaCommands.Play);
            CommandBindings.Add(playBinding);
            playBinding.CanExecute += new CanExecuteRoutedEventHandler(playCommandBinding_CanExecute);
            playBinding.Executed += new ExecutedRoutedEventHandler(playCommandBinding_Executed);

            CommandBinding pauseBinding = new CommandBinding(MediaCommands.Pause);
            CommandBindings.Add(pauseBinding);
            pauseBinding.CanExecute += new CanExecuteRoutedEventHandler(pauseCommandBinding_CanExecute);
            pauseBinding.Executed += new ExecutedRoutedEventHandler(pauseCommandBinding_Executed);

            CommandBinding playToggleBinding = new CommandBinding(Commands.PlayToggle);
            CommandBindings.Add(playToggleBinding);
            playToggleBinding.Executed += new ExecutedRoutedEventHandler(playToggleBinding_Executed);

            // Execute the following code after window and controls are fully loaded and initialized
            // http://stackoverflow.com/a/1746975
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)delegate {
                multiTrackViewer1.ItemsSource = trackList;
                multiTrackViewer1.Matches.Add(match);
                multiTrackViewer1.SelectedMatch = match;
                this.Focus();
            });
            // the following must be called separately on the dispatcher, else the track controls are not initialized yet
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)delegate {
                ZoomToMatch();
            });


            // INIT PLAYER
            player = new MultitrackPlayer(trackList);
            player.VolumeAnnounced += new EventHandler<StreamVolumeEventArgs>(delegate(object sender2, StreamVolumeEventArgs e2) {
                Dispatcher.BeginInvoke((Action)delegate {
                    if (e2.MaxSampleValues.Length >= 2) {
                        stereoVUMeter1.AmplitudeLeft = e2.MaxSampleValues[0];
                        stereoVUMeter1.AmplitudeRight = e2.MaxSampleValues[1];
                    }
                });
            });

            player.CurrentTimeChanged += new EventHandler<ValueEventArgs<TimeSpan>>(
                delegate(object sender2, ValueEventArgs<TimeSpan> e2) {
                    multiTrackViewer1.Dispatcher.BeginInvoke((Action)delegate {
                        multiTrackViewer1.VirtualCaretOffset = e2.Value.Ticks;
                        // autoscroll
                        if (multiTrackViewer1.VirtualViewportInterval.To <= multiTrackViewer1.VirtualCaretOffset) {
                            multiTrackViewer1.VirtualViewportOffset = multiTrackViewer1.VirtualCaretOffset;
                        }
                    });
                });

            player.PlaybackStateChanged += new EventHandler(
                delegate(object sender2, EventArgs e2) {
                    multiTrackViewer1.Dispatcher.BeginInvoke((Action)delegate {
                        // CommandManager must be called on the GUI-thread, else it won't do anything
                        CommandManager.InvalidateRequerySuggested();
                    });
                });

            volumeSlider.ValueChanged += new RoutedPropertyChangedEventHandler<double>(
                delegate(object sender2, RoutedPropertyChangedEventArgs<double> e2) {
                    player.Volume = (float)e2.NewValue;
                });
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
            Task.Factory.StartNew(() => {
                CrossCorrelation.Result result;
                Match ccm = CrossCorrelation.Adjust(match, ProgressMonitor.GlobalInstance, out result);
                Dispatcher.BeginInvoke((Action)delegate {
                    multiTrackViewer1.Matches.Add(ccm);
                    multiTrackViewer1.RefreshAdornerLayer();
                        
                    new CrossCorrelationResult(result) {
                        Owner = this
                    }.Show();
                });
            });
        }
    }
}
