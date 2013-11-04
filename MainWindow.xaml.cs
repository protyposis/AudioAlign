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
using AudioAlign.Audio.Matching.HaitsmaKalker2002;
using AudioAlign.Audio.Streams;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace AudioAlign {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        private Project project;
        private TrackList<AudioTrack> trackList;
        private MultitrackPlayer player;
        private FFTAnalyzer fftAnalyzer;
        private MatchingWindow matchingWindow;
        private AnalysisWindow analysisWindow;

        private int fftAnalyzerConsumer;
        private int correlationConsumer;

        public MainWindow() {
            project = new Project();
            trackList = new TrackList<AudioTrack>();

            InitializeComponent();
        }

        public TrackList<AudioTrack> TrackList {
            get { return trackList; }
        }

        private void multiTrackViewer1_Drop(object sender, DragEventArgs e) {
            // source: http://stackoverflow.com/questions/332859/detect-dragndrop-file-in-wpf
            if (e.Data is DataObject && ((DataObject)e.Data).ContainsFileDropList()) {
                foreach (string filePath in ((DataObject)e.Data).GetFileDropList()) {
                    FileInfo fileInfo = new FileInfo(filePath);
                    if ((fileInfo.Attributes & FileAttributes.Directory) == FileAttributes.Directory) {
                        AddDirectory(new DirectoryInfo(filePath));
                    }
                    else {
                        AddFile(fileInfo);
                    }
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            multiTrackViewer1.ItemsSource = trackList;

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
            player.VolumeAnnounced += Player_VolumeAnnounced_VolumeMeter;

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
                        trackList.Remove(audioTrack);
                    }
                    e2.Handled = true;
                }
            });


            // INIT FFT
            int fftSize = 1024;
            double correlation = 0;
            fftAnalyzer = new FFTAnalyzer(fftSize);
            fftAnalyzerConsumer = 2;
            correlationConsumer = 1;
            WindowFunction fftWindow = WindowUtil.GetFunction(WindowType.BlackmanHarris, fftSize);
            fftAnalyzer.WindowFunction = fftWindow;
            fftAnalyzer.WindowAnalyzed += FftAnalyzer_WindowAnalyzed_FrequencyGraph;
            fftAnalyzer.WindowAnalyzed += FftAnalyzer_WindowAnalyzed_Spectrogram;
            player.SamplesMonitored += new EventHandler<StreamDataMonitorEventArgs>(delegate(object sender2, StreamDataMonitorEventArgs e2) {
                if (fftAnalyzerConsumer > 0 || correlationConsumer > 0) {
                    float[][] uninterleaved = AudioUtil.Uninterleave(e2.Properties, e2.Buffer, e2.Offset, e2.Length, true);
                    if (fftAnalyzerConsumer > 0) {
                        fftAnalyzer.PutSamples(uninterleaved[0]); // put the summed up mono samples into the analyzer
                    }
                    if (correlationConsumer > 0) {
                        correlation = CrossCorrelation.Correlate(uninterleaved[1], uninterleaved[2]);
                        Dispatcher.BeginInvoke((Action)delegate {
                            correlationMeter.Value = correlation;
                        });
                    }
                }
            });
            spectrogram.SpectrogramSize = fftSize / 2;
        }

        private void Player_VolumeAnnounced_VolumeMeter(object sender, StreamVolumeEventArgs e) {
            Dispatcher.BeginInvoke((Action)delegate {
                if (e.MaxSampleValues.Length >= 2) {
                    stereoVUMeter1.AmplitudeLeft = e.MaxSampleValues[0];
                    stereoVUMeter1.AmplitudeRight = e.MaxSampleValues[1];
                }
            });
        }

        private void FftAnalyzer_WindowAnalyzed_FrequencyGraph(object sender, ValueEventArgs<float[]> e) {
            Dispatcher.BeginInvoke((Action)delegate {
                spectrumGraph.Values = e.Value;
            });
        }

        private void FftAnalyzer_WindowAnalyzed_Spectrogram(object sender, ValueEventArgs<float[]> e) {
            Dispatcher.BeginInvoke((Action)delegate {
                spectrogram.AddSpectrogramColumn(e.Value);
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

        private void btnFindMatches_Click(object sender, RoutedEventArgs e) {
            if (matchingWindow == null || !matchingWindow.IsLoaded) {
                matchingWindow = new MatchingWindow(trackList, multiTrackViewer1);
                matchingWindow.Owner = this;
                matchingWindow.Show();
            }
            else {
                matchingWindow.Activate();
            }
            
        }

        private void btnAnalyze_Click(object sender, RoutedEventArgs e) {
            if (analysisWindow == null || !analysisWindow.IsLoaded) {
                analysisWindow = new AnalysisWindow(trackList);
                analysisWindow.Owner = this;
                analysisWindow.Show();
            }
            else {
                analysisWindow.Activate();
            }
        }


        private void btnAlignmentGraph_Click(object sender, RoutedEventArgs e) {
            List<MatchPair> trackPairs = MatchProcessor.GetTrackPairs(trackList);
            MatchProcessor.AssignMatches(trackPairs, multiTrackViewer1.Matches);
            AlignmentGraphWindow window = new AlignmentGraphWindow(trackPairs);
            window.Owner = this;
            window.Show();
        }

        private void SaveProject(FileInfo targetFile) {
            Project p = new Project();
            foreach (AudioTrack track in trackList) {
                p.AudioTracks.Add(track);
            }
            p.Matches.AddRange(multiTrackViewer1.Matches);
            p.MasterVolume = (float)volumeSlider.Value;
            Project.Save(p, targetFile);
            this.project = p;
        }

        private void OpenProject(Project project, bool clear) {
            this.project = project;

            if (clear) {
                // clear current data
                multiTrackViewer1.Matches.Clear();
                trackList.Clear();
            }

            // load new data
            foreach (AudioTrack track in project.AudioTracks) {
                trackList.Add(track);
            }
            foreach (Match match in project.Matches) {
                multiTrackViewer1.Matches.Add(match);
            }
            volumeSlider.Value = project.MasterVolume;

            // update gui
            ResetAudioMonitors();
            multiTrackViewer1.RefreshAdornerLayer(); // TODO find out why this doesn't work
        }

        private void ResetAudioMonitors() {
            spectrumGraph.Reset();
            spectrogram.Reset();
            stereoVUMeter1.Reset();
            correlationMeter.Reset();
        }

        private void AddFile(FileInfo fileInfo) {
            if (AudioStreamFactory.IsSupportedFile(fileInfo.FullName)) {
                AudioTrack audioTrack = new AudioTrack(fileInfo);
                trackList.Add(audioTrack);
            }
        }

        private void AddDirectory(DirectoryInfo dirInfo) {
            foreach (FileInfo fileInfo in dirInfo.EnumerateFiles()) {
                AddFile(fileInfo);
            }
            foreach (DirectoryInfo subDirInfo in dirInfo.EnumerateDirectories()) {
                AddDirectory(subDirInfo);
            }
        }

        /// <summary>
        /// (Re)orders tracks in the tracklist (and timeline) according to a passed in order.
        /// </summary>
        /// <param name="order">the target order of tracks</param>
        private void OrderTracks(IEnumerable<AudioTrack> order) {
            var sortedTracks = order;
            int index = 0;
            foreach (AudioTrack t in sortedTracks) {
                int oldIndex = trackList.IndexOf(t);
                int newIndex = index++;
                trackList.Move(oldIndex, newIndex);
            }
            multiTrackViewer1.RefreshAdornerLayer();
        }

        private void CommandBinding_New(object sender, ExecutedRoutedEventArgs e) {
            OpenProject(new Project(), true);
        }

        private void CommandBinding_Open(object sender, ExecutedRoutedEventArgs e) {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".aap";
            dlg.Filter = "AudioAlign Projects|*.aap";
            dlg.Multiselect = false;

            if (dlg.ShowDialog() == true) {
                OpenProject(Project.Load(new FileInfo(dlg.FileName)), e.Command == ApplicationCommands.Open);
            }
        }

        private void CommandBinding_CanSave(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = project.File != null;
        }

        private void CommandBinding_Save(object sender, ExecutedRoutedEventArgs e) {
            if (project.File != null) {
                SaveProject(project.File);
            }
        }

        private void CommandBinding_SaveAs(object sender, ExecutedRoutedEventArgs e) {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.DefaultExt = ".aap";
            dlg.Filter = "AudioAlign Projects|*.aap";

            if (dlg.ShowDialog() == true) {
                SaveProject(new FileInfo(dlg.FileName));
            }
        }

        private void CommandBinding_FileExportVegasEDL(object sender, ExecutedRoutedEventArgs e) {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.DefaultExt = ".txt";
            dlg.Filter = "Sony Vegas EDL text file|*.txt";

            if (dlg.ShowDialog() == true) {
                Project.ExportEDL(trackList, new FileInfo(dlg.FileName));
            }
        }

        private void CommandBinding_AddAudioFile(object sender, ExecutedRoutedEventArgs e) {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".wav";
            dlg.Filter = "Wave files|*.wav";
            dlg.Multiselect = true;

            if (dlg.ShowDialog() == true) {
                foreach (string fileName in dlg.FileNames) {
                    AddFile(new FileInfo(fileName));
                }
            }
        }

        private void CommandBinding_Close(object sender, ExecutedRoutedEventArgs e) {
            Close();
        }

        private void CommandBinding_DebugRefreshMultiTrackViewer(object sender, ExecutedRoutedEventArgs e) {
            multiTrackViewer1.RefreshAdornerLayer();
        }

        private void CommandBinding_ViewZoomToFit(object sender, ExecutedRoutedEventArgs e) {
            if (trackList.Count > 0) {
                multiTrackViewer1.VirtualViewportOffset = 0;
                multiTrackViewer1.VirtualViewportWidth = trackList.End.Ticks + TimeUtil.SECS_TO_TICKS * 10;
            }
            else {
                MessageBox.Show(this, "Timeline is empty!", ((RoutedUICommand)e.Command).Text, MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        private void CommandBinding_ViewFitTracksVertically(object sender, ExecutedRoutedEventArgs e) {
            multiTrackViewer1.FitTracksVertically(multiTrackViewer1.DisplayTrackHeaders ? 40 : 25);
        }

        private void CommandBinding_ViewGroupMatchingTracks(object sender, ExecutedRoutedEventArgs e) {
            List<MatchGroup> matchGroups = MatchProcessor.DetermineMatchGroups(
                MatchFilterMode.First, trackList, new List<Match>(multiTrackViewer1.Matches), false, TimeSpan.Zero);
            List<AudioTrack> currentOrder = new List<AudioTrack>(trackList);
            List<AudioTrack> targetOrder = new List<AudioTrack>();

            foreach (MatchGroup matchGroup in matchGroups) {
                /* Extract all tracks belonging to a matching group in the order they're contained
                 * in the current tracklist and add them to the target order list. This groups
                 * tracks by their mathing groups while preserving their relative order inside their
                 * group. E.g., if the tracks have been sorted by offset (start time), their intra group
                 * ordering by offset will be kept after being grouped together. */
                targetOrder.AddRange(currentOrder.FindAll(t => matchGroup.TrackList.Contains(t)));
            }

            OrderTracks(targetOrder);
        }

        private void CommandBinding_ViewOrderTracksByOffset(object sender, ExecutedRoutedEventArgs e) {
            OrderTracks(trackList.OrderBy(track => track.Offset));
        }

        private void CommandBinding_ViewOrderTracksByLength(object sender, ExecutedRoutedEventArgs e) {
            OrderTracks(trackList.OrderBy(track => track.Length));
        }

        private void CommandBinding_ViewOrderTracksByName(object sender, ExecutedRoutedEventArgs e) {
            OrderTracks(trackList.OrderBy(track => track.Name));
        }

        private void CommandBinding_ViewDisplayMatches(object sender, ExecutedRoutedEventArgs e) {
            multiTrackViewer1.DisplayMatches = !multiTrackViewer1.DisplayMatches;
        }

        private void CommandBinding_ViewDisplayTrackHeaders(object sender, ExecutedRoutedEventArgs e) {
            multiTrackViewer1.DisplayTrackHeaders = !multiTrackViewer1.DisplayTrackHeaders;
        }

        private void CommandBinding_ViewTimelineScreenshotVisible(object sender, ExecutedRoutedEventArgs e) {
            multiTrackViewer1.CopyToClipboard(false);
        }

        private void CommandBinding_ViewTimelineScreenshotFull(object sender, ExecutedRoutedEventArgs e) {
            multiTrackViewer1.CopyToClipboard(true);
        }

        private void CommandBinding_MonitorMasterVolume(object sender, ExecutedRoutedEventArgs e) {
            if (menuItemMonitorMasterVolume.IsChecked) {
                player.VolumeAnnounced += Player_VolumeAnnounced_VolumeMeter;
            }
            else {
                player.VolumeAnnounced -= Player_VolumeAnnounced_VolumeMeter;
                stereoVUMeter1.Reset();
            }
        }

        private void CommandBinding_MonitorMasterCorrelation(object sender, ExecutedRoutedEventArgs e) {
            if (menuItemMonitorMasterCorrelation.IsChecked) {
                correlationConsumer++;
            }
            else {
                correlationMeter.Reset();
                correlationConsumer--;
            }
        }

        private void CommandBinding_MonitorFrequencyGraph(object sender, ExecutedRoutedEventArgs e) {
            if (menuItemMonitorFrequencyGraph.IsChecked) {
                fftAnalyzer.WindowAnalyzed += FftAnalyzer_WindowAnalyzed_FrequencyGraph;
                fftAnalyzerConsumer++;
            }
            else {
                fftAnalyzer.WindowAnalyzed -= FftAnalyzer_WindowAnalyzed_FrequencyGraph;
                fftAnalyzerConsumer--;
                spectrumGraph.Reset();
            }
        }

        private void CommandBinding_MonitorSpectrogram(object sender, ExecutedRoutedEventArgs e) {
            if (menuItemMonitorSpectrogram.IsChecked) {
                fftAnalyzer.WindowAnalyzed += FftAnalyzer_WindowAnalyzed_Spectrogram;
                fftAnalyzerConsumer++;
            }
            else {
                fftAnalyzer.WindowAnalyzed -= FftAnalyzer_WindowAnalyzed_Spectrogram;
                fftAnalyzerConsumer--;
                spectrogram.Reset();
            }
        }

        private void CommandBinding_TracksUnlockAll(object sender, ExecutedRoutedEventArgs e) {
            foreach (AudioTrack track in trackList) {
                track.Locked = false;
            }
        }
    }
}
