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
using Aurio.Project;
using System.IO;
using Aurio.WaveControls;
using Aurio;
using System.Diagnostics;
using System.Windows.Threading;
using Aurio.TaskMonitor;
using Aurio.Matching;
using Aurio.Matching.HaitsmaKalker2002;
using Aurio.Streams;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace AudioAlign {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        private RecentProjects recentProjects;
        private Project project;
        private TrackList<AudioTrack> trackList;
        private MultitrackPlayer player;
        private FFTAnalyzer fftAnalyzer;
        private MatchingWindow matchingWindow;
        private AnalysisWindow analysisWindow;

        private int fftAnalyzerConsumer;
        private int correlationConsumer;

        public MainWindow() {
            recentProjects = new RecentProjects();

            /*
             * The menu items of the recently opened project are handled here in code behind 
             * because in XAML (ItemsSource/CompositeCollection/CollectionViewSource/CollectionContainer) 
             * there's a few problems I spent too much time with and could not solve. This
             * programmatic solution works perfectly.
             * 
             * - When overriding the ItemContainerStyle, which is necessary to automatically
             *   set the Header and Command, the style breaks... this is probably because of
             *   the weird style combination I'm using in this app and wouldn't happen with
             *   the default style (setting ItemContainerStyle replaces the style in the locally
             *   included style file and falls back to proerties of the default style for the app).
             * - When setting the header text through the style, the header text of all statically 
             *   defined MenuItems gets overwritten because they don't have the header directly
             *   set but get the header text from the associated command. So the header text
             *   overrules the command text.
             */
            recentProjects.MenuEntries.CollectionChanged += delegate(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
                // Always rebuilt the whole menu... happens very seldomly and shouldn't be a noticable performance impact
                int separatorIndex = FileMenu.Items.IndexOf(RecentSeparator);

                // Clear old entries
                if (FileMenu.Items.Count > (separatorIndex + 1)) {
                    for (int x = FileMenu.Items.Count - 1; x > separatorIndex; x--) {
                        FileMenu.Items.RemoveAt(x);
                    }
                }

                Debug.Assert(FileMenu.Items.Count == separatorIndex + 1, "wrong menu delete count");

                // Add new entries
                int count = 0;
                foreach (RecentProjects.RecentEntry entry in recentProjects.MenuEntries) {
                    // Determine if this item represents a real project to be numbered
                    bool projectEntry = entry.Parameter != null && entry.Parameter != RecentProjects.ClearCommand;

                    FileMenu.Items.Add(new MenuItem {
                        Header = (projectEntry ? (++count) + " " : "") + entry.Title.Replace("_", "__"),
                        IsEnabled = entry.Enabled,
                        Command = Commands.FileOpenRecentProject,
                        CommandParameter = entry.Parameter
                    });
                }

                Debug.Assert(FileMenu.Items.Count == separatorIndex + 1 + recentProjects.MenuEntries.Count, "wrong menu item count");
            };

            project = new Project();
            trackList = new TrackList<AudioTrack>();

            InitializeComponent();
            recentProjects.Load();
        }

        public TrackList<AudioTrack> TrackList {
            get { return trackList; } // required for binding to the GUI
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
            Title = Title + (Environment.Is64BitProcess ? " (x64)" : " (x86)");

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


            //// INIT TRACKLIST STUFF
            //trackList.PropertyChanged += delegate(object sender2, PropertyChangedEventArgs e2) {
            //    if (e2.PropertyName == "TotalLength") {
            //        multiTrackViewer1.VirtualViewportMaxWidth = trackList.TotalLength.Ticks;
            //    }
            //};


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
                    // create temporary list to avoid concurrent modification exception
                    var selectedAudioTracks = new List<AudioTrack>(multiTrackViewer1.SelectedItems.Cast<AudioTrack>());

                    // delete tracks
                    foreach (AudioTrack audioTrack in selectedAudioTracks) {
                        if (audioTrack != null) {
                            // 1. delete all related matches
                            List<Match> deleteList = new List<Match>();
                            // 1a find all related matches
                            foreach (Match m in multiTrackViewer1.Matches) {
                                if (m.Track1 == audioTrack || m.Track2 == audioTrack) {
                                    deleteList.Add(m);
                                }
                            }
                            // 1b delete
                            foreach (Match m in deleteList) {
                                multiTrackViewer1.Matches.Remove(m);
                            }
                            // 2. delete track
                            trackList.Remove(audioTrack);
                        }
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

            recentProjects.Add(targetFile.FullName);
            recentProjects.Save();
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

            if (project.File != null) {
                recentProjects.Add(project.File.FullName);
                recentProjects.Save();
            }

            CommandBinding_ViewZoomToFit(this, null);
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
                ShowStatus("Saved", true);
            }
        }

        private void CommandBinding_SaveAs(object sender, ExecutedRoutedEventArgs e) {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.DefaultExt = ".aap";
            dlg.Filter = "AudioAlign Projects|*.aap";

            if (dlg.ShowDialog() == true) {
                SaveProject(new FileInfo(dlg.FileName));
                ShowStatus("Saved", true);
            }
        }

        private void CommandBinding_FileExportVegasEDL(object sender, ExecutedRoutedEventArgs e) {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.DefaultExt = ".txt";
            dlg.Filter = "Sony Vegas EDL text file|*.txt";

            if (dlg.ShowDialog() == true) {
                Project.ExportEDL(trackList, new FileInfo(dlg.FileName));
                ShowStatus("Vegas EDL Exported", true);
            }
        }

        private void CommandBinding_FileExportSyncXML(object sender, ExecutedRoutedEventArgs e) {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.DefaultExt = ".xml";
            dlg.Filter = "Sync XML|*.xml";

            if (dlg.ShowDialog() == true) {
                Project p = new Project();
                foreach (AudioTrack track in trackList) {
                    p.AudioTracks.Add(track);
                }
                p.Matches.AddRange(multiTrackViewer1.Matches);
                p.MasterVolume = (float)volumeSlider.Value;
                Project.ExportSyncXML(p, new FileInfo(dlg.FileName));
                ShowStatus("Sync XML Exported", true);
            }

            //JikuDatasetUtils.EvaluateOffsets(trackList);
        }

        private void CommandBinding_FileExportMatchesCSV(object sender, ExecutedRoutedEventArgs e) {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.DefaultExt = ".csv";
            dlg.Filter = "Matches CSV|*.csv";

            if (dlg.ShowDialog() == true) {
                Project.ExportMatchesCSV(multiTrackViewer1.Matches, new FileInfo(dlg.FileName));
                ShowStatus("Matches CSV Exported", true);
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

        private void CommandBinding_FileExportAudioMix(object sender, ExecutedRoutedEventArgs e) {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.DefaultExt = ".wav";
            dlg.Filter = "Wave files|*.wav";

            if (dlg.ShowDialog() == true) {
                // create a separate monitor just for the file export to isolate this progress for the 
                // modal window (else other running tasks could be shown in the modal dialog too)
                var progressMonitor = new ProgressMonitor();
                ProgressMonitor.GlobalInstance.AddChild(progressMonitor);

                // progress window needs to be open before beginning a task (else progress bar init does not get called)
                var modalProgress = new ModalProgressWindow(progressMonitor);
                modalProgress.Owner = this;
                modalProgress.Show();

                var progressReporter = progressMonitor.BeginTask("Rendering mix to file...", true);

                Task.Factory.StartNew(() => {
                    player.SaveToFile(new FileInfo(dlg.FileName), progressReporter);

                    Dispatcher.BeginInvoke((Action)delegate {
                        progressReporter.Finish();
                        modalProgress.Close();
                        ProgressMonitor.GlobalInstance.RemoveChild(progressMonitor);
                        ShowStatus("Audio export finished", true);
                    });
                });
            }
        }

        private void CommandBinding_FileExportSelectedTracks(object sender, ExecutedRoutedEventArgs e) {
            // Get the list of selected tracks
            var selectedTracks = multiTrackViewer1.SelectedItems.Cast<AudioTrack>().ToList();

            if (selectedTracks.Count == 0) {
                ShowStatus("No tracks selected!", false);
                return;
            }

            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.DefaultExt = ".wav";
            dlg.Filter = "Wave files|*.wav";
            dlg.FileName = "AAexport {name}";

            if (dlg.ShowDialog() == true) {
                // create a separate monitor just for the file export to isolate this progress for the 
                // modal window (else other running tasks could be shown in the modal dialog too)
                var progressMonitor = new ProgressMonitor();
                ProgressMonitor.GlobalInstance.AddChild(progressMonitor);

                // progress window needs to be open before beginning a task (else progress bar init does not get called)
                var modalProgress = new ModalProgressWindow(progressMonitor);
                modalProgress.Owner = this;
                modalProgress.Show();

                Task.Factory.StartNew(() => {
                    foreach (AudioTrack track in selectedTracks) {
                        var progressReporter = progressMonitor.BeginTask("Rendering " + track.Name + " to file...", true);
                        player.SaveToFile(track, new FileInfo(dlg.FileName.Replace("{name}", track.Name)), progressReporter);
                        progressReporter.Finish();
                    }

                    Dispatcher.BeginInvoke((Action)delegate {
                        modalProgress.Close();
                        ProgressMonitor.GlobalInstance.RemoveChild(progressMonitor);
                        ShowStatus("Audio export finished", true);
                    });
                });
            }
        }

        private void CommandBinding_Close(object sender, ExecutedRoutedEventArgs e) {
            Close();
        }

        private void CommandBinding_FileOpenRecentProject(object sender, ExecutedRoutedEventArgs e) {
            if ((string)e.Parameter == RecentProjects.ClearCommand) {
                recentProjects.Clear();
            }
            else {
                OpenProject(Project.Load(new FileInfo((string)e.Parameter)), true);
            }
        }

        private void CommandBinding_DebugRefreshMultiTrackViewer(object sender, ExecutedRoutedEventArgs e) {
            multiTrackViewer1.RefreshAdornerLayer();
        }

        private void CommandBinding_DebugRefreshPeakStores(object sender, ExecutedRoutedEventArgs e) {
            // The peaks get recalculated when the length of a track changes, which currently
            // only happens when it gets warped. By setting the length to its own value, we
            // do not change the track but trigger the event that leads to a peak refresh.
            trackList.ToList().ForEach(track => track.Length = track.Length);
        }

        private void CommandBinding_ViewZoomToFit(object sender, ExecutedRoutedEventArgs e) {
            if (trackList.Count > 0) {
                multiTrackViewer1.VirtualViewportOffset = 0;
                multiTrackViewer1.VirtualViewportWidth = trackList.End.Ticks + TimeUtil.SECS_TO_TICKS * 10;
            }
            else {
                multiTrackViewer1.VirtualViewportOffset = 0;
                multiTrackViewer1.VirtualViewportWidth = TimeUtil.SECS_TO_TICKS * 60 * 10;

                if (e != null) {
                    MessageBox.Show(this, "Timeline is empty!", ((RoutedUICommand)e.Command).Text, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
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

        private void CommandBinding_TracksUnmuteAll(object sender, ExecutedRoutedEventArgs e) {
            foreach (AudioTrack track in trackList) {
                track.Mute = false;
            }
        }

        private void CommandBinding_TracksUnsoloAll(object sender, ExecutedRoutedEventArgs e) {
            foreach (AudioTrack track in trackList) {
                track.Solo = false;
            }
        }

        private void CommandBinding_TracksUnlockAll(object sender, ExecutedRoutedEventArgs e) {
            foreach (AudioTrack track in trackList) {
                track.Locked = false;
            }
        }

        private void CommandBinding_TracksResetVolume(object sender, ExecutedRoutedEventArgs e) {
            foreach (AudioTrack track in trackList) {
                track.Volume = 1f;
            }
        }

        private void CommandBinding_TracksResetColors(object sender, ExecutedRoutedEventArgs e) {
            foreach (AudioTrack track in trackList) {
                track.Color = Track.DEFAULT_COLOR;
            }
        }

        private void CommandBinding_OpenAboutBox(object sender, ExecutedRoutedEventArgs e) {
            new About() { Owner = this }.ShowDialog();
        }

        /// <summary>
        /// Displays a status message in the status bar to provide the user feedback for operations
        /// without visible results. The message disappears automatically after a few seconds.
        /// </summary>
        /// <param name="text">the text to display</param>
        /// <param name="displayTime">if true, the current time is attached to the text</param>
        public void ShowStatus(string text, bool displayTime) {
            statusLabel.Content = (displayTime ? DateTime.Now.ToShortTimeString() + " " : " ") + text;
            statusLabel.Visibility = System.Windows.Visibility.Visible;
            var timer = statusLabel.Tag as DispatcherTimer;
            if (timer == null)
            {
                statusLabel.Tag = timer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 3) };
                timer.Tick += delegate(object sender, EventArgs e)
                {
                    timer.Stop();
                    statusLabel.Content = "";
                    statusLabel.Visibility = System.Windows.Visibility.Collapsed;
                };
            }
            else
            {
                timer.Stop(); // stop the timer if it is still active from the previous call
            }
            timer.Start();
        }
    }
}
