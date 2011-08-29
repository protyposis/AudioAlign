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
using System.Windows.Navigation;
using System.Windows.Shapes;
using AudioAlign.Audio.Project;
using System.IO;
using AudioAlign.WaveControls;
using AudioAlign.Audio;
using System.Diagnostics;
using System.Windows.Threading;
using AudioAlign.Audio.TaskMonitor;
using AudioAlign.Audio.Matching;
using NAudio.Wave;
using AudioAlign.Audio.Matching.HaitsmaKalker2002;
using AudioAlign.Audio.Streams;
using System.Threading.Tasks;

namespace AudioAlign {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        private TrackList<AudioTrack> trackList;
        private MultitrackPlayer player;
        private MatchingWindow matchingWindow;
        private AnalysisWindow analysisWindow;

        public MainWindow() {
            InitializeComponent();

            trackList = new TrackList<AudioTrack>();
            Button test = btnAddWaveform;
        }

        private void btnAddWaveform_Click(object sender, RoutedEventArgs e) {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".wav";
            dlg.Filter = "Wave files|*.wav";
            dlg.Multiselect = true;

            if (dlg.ShowDialog() == true) {
                foreach (string fileName in dlg.FileNames) {
                    AddFile(fileName);
                }
            }
        }

        private void AddFile(string fileName) {
            if (AudioStreamFactory.IsSupportedFile(fileName)) {
                AudioTrack audioTrack = new AudioTrack(new FileInfo(fileName));
                multiTrackViewer1.Items.Add(audioTrack);
                trackList.Add(audioTrack);
            }
        }

        private void multiTrackViewer1_Drop(object sender, DragEventArgs e) {
            // source: http://stackoverflow.com/questions/332859/detect-dragndrop-file-in-wpf
            if (e.Data is DataObject && ((DataObject)e.Data).ContainsFileDropList()) {
                foreach (string filePath in ((DataObject)e.Data).GetFileDropList()) {
                    AddFile(filePath);
                }
            }
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


            // INIT PLAYER
            player = new MultitrackPlayer(trackList);

            player.VolumeAnnounced += new EventHandler<AudioAlign.Audio.Streams.StreamVolumeEventArgs>(
                delegate(object sender2, AudioAlign.Audio.Streams.StreamVolumeEventArgs e2) {
                    multiTrackViewer1.Dispatcher.BeginInvoke((Action)delegate {
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


            // INIT PROGRESSBAR
            progressBar1.IsEnabled = false;
            ProgressMonitor.GlobalInstance.ProcessingStarted += new EventHandler(
                delegate(object sender2, EventArgs e2) {
                    progressBar1.Dispatcher.BeginInvoke((Action)delegate {
                        progressBar1.IsEnabled = true;
                        progressBar1Label.Text = ProgressMonitor.GlobalInstance.StatusMessage;
                        win7TaskBar.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;
                        win7TaskBar.ProgressValue = 0;
                    });
                });

            ProgressMonitor.GlobalInstance.ProcessingProgressChanged += new EventHandler<ValueEventArgs<float>>(
                delegate(object sender2, ValueEventArgs<float> e2) {
                    progressBar1.Dispatcher.BeginInvoke((Action)delegate {
                        progressBar1.Value = e2.Value;
                        win7TaskBar.ProgressValue = e2.Value / 100;
                        progressBar1Label.Text = ProgressMonitor.GlobalInstance.StatusMessage;
                    });
                });

            ProgressMonitor.GlobalInstance.ProcessingFinished += new EventHandler(
                delegate(object sender2, EventArgs e2) {
                    progressBar1.Dispatcher.BeginInvoke((Action)delegate {
                        progressBar1.Value = 0;
                        progressBar1.IsEnabled = false;
                        progressBar1Label.Text = "";
                        win7TaskBar.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
                    });
                });


            // INIT RANDOM STUFF
            multiTrackViewer1.KeyUp += new KeyEventHandler(delegate(object sender2, KeyEventArgs e2) {
                if (e2.Key == Key.Delete) {
                    AudioTrack audioTrack = multiTrackViewer1.SelectedItem as AudioTrack;
                    if (audioTrack != null) {
                        multiTrackViewer1.Items.Remove(audioTrack);
                        trackList.Remove(audioTrack);
                    }
                    e2.Handled = true;
                }
            });


            // INIT FFT
            FFTAnalyzer fftAnalyzer = new FFTAnalyzer(1024);
            WindowFunction fftWindow = WindowUtil.GetFunction(WindowType.BlackmanHarris, 1024);
            fftAnalyzer.WindowFunction = fftWindow;
            fftAnalyzer.WindowAnalyzed += new EventHandler<ValueEventArgs<float[]>>(delegate(object sender2, ValueEventArgs<float[]> e2) {
                spectrumGraph.Dispatcher.BeginInvoke((Action)delegate {
                    spectrumGraph.Values = e2.Value;
                });
            });
            player.SamplesMonitored += new EventHandler<StreamDataMonitorEventArgs>(delegate(object sender2, StreamDataMonitorEventArgs e2) {
                fftAnalyzer.PutSamples(AudioUtil.Uninterleave(e2.Properties, e2.Buffer, e2.Offset, e2.Length)[0]);
            });
        }

        private void Window_Closed(object sender, EventArgs e) {
            player.Dispose();
            if (matchingWindow != null) {
                matchingWindow.Close();
            }
            if (analysisWindow != null) {
                analysisWindow.Close();
            }
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

        private void btnRefresh_Click(object sender, RoutedEventArgs e) {
            multiTrackViewer1.RefreshAdornerLayer();
        }

        private void btnFindMatches_Click(object sender, RoutedEventArgs e) {
            if (matchingWindow == null || !matchingWindow.IsLoaded) {
                matchingWindow = new MatchingWindow(trackList, multiTrackViewer1);
                matchingWindow.Show();
            }
            else {
                matchingWindow.Activate();
            }
            
        }

        private void btnAnalyze_Click(object sender, RoutedEventArgs e) {
            if (analysisWindow == null || !analysisWindow.IsLoaded) {
                analysisWindow = new AnalysisWindow(trackList);
                analysisWindow.Show();
            }
            else {
                analysisWindow.Activate();
            }
        }
    }
}
