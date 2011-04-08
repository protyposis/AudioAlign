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
using AudioAlign.Audio.TaskMonitor;
using AudioAlign.Audio;
using AudioAlign.Audio.Matching.HaitsmaKalker2002;
using AudioAlign.WaveControls;
using AudioAlign.Audio.Project;
using AudioAlign.Audio.Matching;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Interop;

namespace AudioAlign {
    /// <summary>
    /// Interaction logic for MatchingWindow.xaml
    /// </summary>
    public partial class MatchingWindow : Window {

        private FingerprintStore fingerprintStore;
        private TrackList<AudioTrack> trackList;
        private MultiTrackViewer multiTrackViewer;

        private volatile int numTasksRunning;

        public MatchingWindow(TrackList<AudioTrack> trackList, MultiTrackViewer multiTrackViewer) {
            InitializeComponent();
            this.fingerprintStore = new FingerprintStore();
            this.trackList = trackList;
            this.multiTrackViewer = multiTrackViewer;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            // GLASS EFFECT BACKGROUND
            // http://msdn.microsoft.com/en-us/library/ms748975.aspx
            try {
                // Obtain the window handle for WPF application
                IntPtr mainWindowPtr = new WindowInteropHelper(this).Handle;
                HwndSource mainWindowSrc = HwndSource.FromHwnd(mainWindowPtr);
                mainWindowSrc.CompositionTarget.BackgroundColor = Color.FromArgb(0, 0, 0, 0);

                // Set Margins
                NonClientRegionAPI.MARGINS margins = new NonClientRegionAPI.MARGINS();
                margins.cxLeftWidth = -1;
                margins.cxRightWidth = -1;
                margins.cyTopHeight = -1;
                margins.cyBottomHeight = -1;

                int hr = NonClientRegionAPI.DwmExtendFrameIntoClientArea(mainWindowSrc.Handle, ref margins);
                Background = Brushes.Transparent;
                //
                if (hr < 0) {
                    //DwmExtendFrameIntoClientArea Failed
                }
            }
            // If not Vista, paint background white.
            catch (DllNotFoundException) {
                //Application.Current.MainWindow.Background = Brushes.White;
            }

            // INIT PROGRESSBAR
            progressBar.IsEnabled = false;
            ProgressMonitor.Instance.ProcessingStarted += Instance_ProcessingStarted;
            //ProgressMonitor.Instance.ProcessingProgressChanged += Instance_ProcessingProgressChanged;
            ProgressMonitor.Instance.ProcessingFinished += Instance_ProcessingFinished;
            bestMatchRadioButton.IsChecked = true;

            matchGrid.ItemsSource = multiTrackViewer.Matches;
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e) {
            ProgressMonitor.Instance.ProcessingStarted -= Instance_ProcessingStarted;
            ProgressMonitor.Instance.ProcessingProgressChanged -= Instance_ProcessingProgressChanged;
            ProgressMonitor.Instance.ProcessingFinished -= Instance_ProcessingFinished;
            multiTrackViewer.SelectedMatch = null;
        }

        private void Instance_ProcessingStarted(object sender, EventArgs e) {
            progressBar.Dispatcher.BeginInvoke((Action)delegate {
                progressBar.IsEnabled = true;
                progressBar.IsIndeterminate = true;
                //progressBarLabel.Text = ProgressMonitor.Instance.StatusMessage;
            });
        }

        private void Instance_ProcessingProgressChanged(object sender, ValueEventArgs<float> e) {
            progressBar.Dispatcher.BeginInvoke((Action)delegate {
                progressBar.Value = e.Value;
                progressBarLabel.Text = ProgressMonitor.Instance.StatusMessage;
            });
        }

        private void Instance_ProcessingFinished(object sender, EventArgs e) {
            progressBar.Dispatcher.BeginInvoke((Action)delegate {
                progressBar.Value = 0;
                progressBar.IsEnabled = false;
                progressBar.IsIndeterminate = false;
                progressBarLabel.Text = "";
            });
        }

        private void scanTracksButton_Click(object sender, RoutedEventArgs e) {
            // calculate subfingerprints
            numTasksRunning = trackList.Count;
            fingerprintStore.Clear();

            Task.Factory.StartNew(() => Parallel.ForEach<AudioTrack>(trackList, audioTrack => {
                DateTime startTime = DateTime.Now;
                ProgressReporter progressReporter = ProgressMonitor.Instance.BeginTask("Generating sub-fingerprints for " + audioTrack.FileInfo.Name, true);

                FingerprintGenerator fpg = new FingerprintGenerator(audioTrack, 3, true);
                int subFingerprintsCalculated = 0;
                fpg.SubFingerprintCalculated += new EventHandler<SubFingerprintEventArgs>(delegate(object s2, SubFingerprintEventArgs e2) {
                    subFingerprintsCalculated++;
                    progressReporter.ReportProgress((double)e2.Timestamp.Ticks / audioTrack.Length.Ticks * 100);
                    fingerprintStore.Add(e2.AudioTrack, e2.SubFingerprint, e2.Timestamp, e2.IsVariation);
                });
                fpg.Completed += new EventHandler(FingerprintGenerator_Completed);
                fpg.Generate();

                ProgressMonitor.Instance.EndTask(progressReporter);
                Debug.WriteLine("subfingerprint generation finished - " + (DateTime.Now - startTime));
            }));
        }

        private void FingerprintGenerator_Completed(object sender, EventArgs e) {
            if (--numTasksRunning == 0) {
                // all running generator tasks have finished
                multiTrackViewer.Dispatcher.BeginInvoke((Action)delegate {
                    // calculate fingerprints / matches after processing of all tracks has finished
                    ClearAllMatches();
                    FindAllDirectMatches();
                });
            }
        }

        private void alignTracksButton_Click(object sender, RoutedEventArgs e) {
            List<Match> matches = new List<Match>(multiTrackViewer.Matches);
            List<Match> selectedMatches = new List<Match>();

            if (matches.Count == 0) {
                Debug.WriteLine("no matches available");
                return;
            }

            Dictionary<AudioTrack, List<Match>> mapping = new Dictionary<AudioTrack, List<Match>>();

            foreach (AudioTrack audioTrack in trackList) {
                if (!mapping.ContainsKey(audioTrack)) {
                    mapping.Add(audioTrack, new List<Match>());
                }
                List<Match> audioTrackMatches = mapping[audioTrack];
                foreach (Match match in matches) {
                    if (match.Track1 == audioTrack || match.Track2 == audioTrack) {
                        audioTrackMatches.Add(match);
                    }
                }
            }

            if ((bool)bestMatchRadioButton.IsChecked) {
                foreach (AudioTrack audioTrack in mapping.Keys) {
                    if (audioTrack == trackList[0] || mapping[audioTrack].Count == 0) {
                        continue;
                    }
                    IEnumerable<Match> sortedMatches = mapping[audioTrack].OrderByDescending(m => m.Similarity);
                    Match bestMatch = sortedMatches.First();
                    Debug.WriteLine("best match: " + bestMatch);
                    selectedMatches.Add(bestMatch);
                }
            }
            else if ((bool)firstMatchRadioButton.IsChecked) {
                foreach (AudioTrack audioTrack in mapping.Keys) {
                    if (audioTrack == trackList[0]) {
                        continue;
                    }
                    IEnumerable<Match> sortedMatches = mapping[audioTrack].OrderBy(m => m.Track1Time);
                    Match firstMatch = sortedMatches.First();
                    Debug.WriteLine("first match: " + firstMatch);
                    selectedMatches.Add(firstMatch);
                }
            }
            else if ((bool)midMatchRadioButton.IsChecked) {
                foreach (AudioTrack audioTrack in mapping.Keys) {
                    if (audioTrack == trackList[0]) {
                        continue;
                    }
                    IEnumerable<Match> sortedMatches = mapping[audioTrack].OrderBy(m => m.Track1Time);
                    Match midMatch = sortedMatches.ElementAt(sortedMatches.Count() / 2);
                    Debug.WriteLine("mid match: " + midMatch);
                    selectedMatches.Add(midMatch);
                }
            }
            else if ((bool)lastMatchRadioButton.IsChecked) {
                foreach (AudioTrack audioTrack in mapping.Keys) {
                    if (audioTrack == trackList[0]) {
                        continue;
                    }
                    IEnumerable<Match> sortedMatches = mapping[audioTrack].OrderBy(m => m.Track1Time);
                    Match lastMatch = sortedMatches.Last();
                    Debug.WriteLine("last match: " + lastMatch);
                    selectedMatches.Add(lastMatch);
                }
            }
            else if ((bool)averageMatchRadioButton.IsChecked) {
                //
            }
            else if ((bool)allMatchRadioButton.IsChecked) {
                //
            }

            foreach (Match selectedMatch in selectedMatches) {
                if (selectedMatch.Track1.Offset.Ticks + selectedMatch.Track1Time.Ticks < selectedMatch.Track2.Offset.Ticks + selectedMatch.Track2Time.Ticks) {
                    // align track 1
                    selectedMatch.Track1.Offset = new TimeSpan(selectedMatch.Track2.Offset.Ticks + selectedMatch.Track2Time.Ticks - selectedMatch.Track1Time.Ticks);
                }
                else {
                    // align track 2
                    selectedMatch.Track2.Offset = new TimeSpan(selectedMatch.Track1.Offset.Ticks + selectedMatch.Track1Time.Ticks - selectedMatch.Track2Time.Ticks);
                }
                if ((bool)postProcessMatchingPointsCheckBox.IsChecked) {
                    CrossCorrelation.Adjust(selectedMatch);
                }
            }
            if ((bool)removeUnusedMatchingPointsCheckBox.IsChecked) {
                multiTrackViewer.Matches.Clear();
                foreach (Match selectedMatch in selectedMatches) {
                    multiTrackViewer.Matches.Add(selectedMatch);
                }
            }
        }

        private void crossCorrelateButton_Click(object sender, RoutedEventArgs e) {
            List<Match> matches = new List<Match>(multiTrackViewer.Matches);
            long secfactor = 1000 * 1000 * 10;

            foreach (Match matchFE in matches) {
                Match match = matchFE; // needed as reference for async task
                Task.Factory.StartNew(() => {
                    TimeSpan offset = CrossCorrelation.Calculate(match.Track1.CreateAudioStream(), new Interval(match.Track1Time.Ticks, match.Track1Time.Ticks + secfactor / 2),
                        match.Track2.CreateAudioStream(), new Interval(match.Track2Time.Ticks, match.Track2Time.Ticks + secfactor / 2));

                    Debug.WriteLine("CC: " + match + ": " + offset);
                });
            }
        }

        private void matchGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            multiTrackViewer.SelectedMatch = matchGrid.SelectedItem as Match;
        }

        private void clearMatchesButton_Click(object sender, RoutedEventArgs e) {
            ClearAllMatches();
        }

        private void findMatchingMatchesButton_Click(object sender, RoutedEventArgs e) {
            FindAllDirectMatches();
        }

        private void ClearAllMatches() {
            multiTrackViewer.Matches.Clear();
        }

        private void FindAllDirectMatches() {
            List<Match> matches = fingerprintStore.FindAllMatchingMatches();
            Debug.WriteLine(matches.Count + " matches found");
            matches = FingerprintStore.FilterDuplicateMatches(matches);
            Debug.WriteLine(matches.Count + " matches found (filtered)");
            foreach (Match match in matches) {
                multiTrackViewer.Matches.Add(match);
            }
        }

        private void FindAllSoftMatches() {
            List<Match> matches = fingerprintStore.FindAllMatches();
            Debug.WriteLine(matches.Count + " matches found");
            matches = FingerprintStore.FilterDuplicateMatches(matches);
            Debug.WriteLine(matches.Count + " matches found (filtered)");
            foreach (Match match in matches) {
                multiTrackViewer.Matches.Add(match);
            }
        }

        private void findSoftMatchesButton_Click(object sender, RoutedEventArgs e) {
            FindAllSoftMatches();
        }

        private void findPossibleMatchesButton_Click(object sender, RoutedEventArgs e) {
            fingerprintStore.FindAllMatches(3, true);
        }

        private void clearStoreButton_Click(object sender, RoutedEventArgs e) {
            fingerprintStore.Clear();
        }
    }
}
