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

        private ProgressMonitor progressMonitor;
        private FingerprintStore fingerprintStore;
        private TrackList<AudioTrack> trackList;
        private MultiTrackViewer multiTrackViewer;

        private volatile int numTasksRunning;

        public MatchingWindow(TrackList<AudioTrack> trackList, MultiTrackViewer multiTrackViewer) {
            InitializeComponent();
            progressMonitor = new ProgressMonitor();
            this.fingerprintStore = new FingerprintStore();
            this.trackList = trackList;
            this.multiTrackViewer = multiTrackViewer;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            NonClientRegionAPI.Glassify(this);

            // INIT PROGRESSBAR
            progressBar.IsEnabled = false;
            progressMonitor.ProcessingStarted += Instance_ProcessingStarted;
            progressMonitor.ProcessingProgressChanged += Instance_ProcessingProgressChanged;
            progressMonitor.ProcessingFinished += Instance_ProcessingFinished;
            ProgressMonitor.GlobalInstance.AddChild(progressMonitor);
            bestMatchRadioButton.IsChecked = true;

            matchGrid.ItemsSource = multiTrackViewer.Matches;
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e) {
            ProgressMonitor.GlobalInstance.RemoveChild(progressMonitor);
            progressMonitor.ProcessingStarted -= Instance_ProcessingStarted;
            progressMonitor.ProcessingProgressChanged -= Instance_ProcessingProgressChanged;
            progressMonitor.ProcessingFinished -= Instance_ProcessingFinished;
            multiTrackViewer.SelectedMatch = null;
        }

        private void Instance_ProcessingStarted(object sender, EventArgs e) {
            progressBar.Dispatcher.BeginInvoke((Action)delegate {
                progressBar.IsEnabled = true;
                progressBarLabel.Text = progressMonitor.StatusMessage;
            });
        }

        private void Instance_ProcessingProgressChanged(object sender, ValueEventArgs<float> e) {
            progressBar.Dispatcher.BeginInvoke((Action)delegate {
                progressBar.Value = e.Value;
                progressBarLabel.Text = progressMonitor.StatusMessage;
            });
        }

        private void Instance_ProcessingFinished(object sender, EventArgs e) {
            progressBar.Dispatcher.BeginInvoke((Action)delegate {
                progressBar.Value = 0;
                progressBar.IsEnabled = false;
                progressBarLabel.Text = "";
            });
        }

        private void scanTracksButton_Click(object sender, RoutedEventArgs e) {
            // calculate subfingerprints
            numTasksRunning = trackList.Count;
            fingerprintStore.Clear();

            Task.Factory.StartNew(() => Parallel.ForEach<AudioTrack>(trackList, audioTrack => {
                DateTime startTime = DateTime.Now;
                IProgressReporter progressReporter = progressMonitor.BeginTask("Generating sub-fingerprints for " + audioTrack.FileInfo.Name, true);

                FingerprintGenerator fpg = new FingerprintGenerator(audioTrack, 3, true);
                int subFingerprintsCalculated = 0;
                fpg.SubFingerprintCalculated += new EventHandler<SubFingerprintEventArgs>(delegate(object s2, SubFingerprintEventArgs e2) {
                    subFingerprintsCalculated++;
                    progressReporter.ReportProgress((double)e2.Timestamp.Ticks / audioTrack.Length.Ticks * 100);
                    fingerprintStore.Add(e2.AudioTrack, e2.SubFingerprint, e2.Timestamp, e2.IsVariation);
                });
                fpg.Completed += new EventHandler(FingerprintGenerator_Completed);
                fpg.Generate();

                progressReporter.Finish();
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
            MatchFilterMode matchFilterMode = MatchFilterMode.None;
            if ((bool)bestMatchRadioButton.IsChecked) {
                 matchFilterMode = MatchFilterMode.Best;
            }
            else if ((bool)firstMatchRadioButton.IsChecked) {
                matchFilterMode = MatchFilterMode.First;
            }
            else if ((bool)midMatchRadioButton.IsChecked) {
                matchFilterMode = MatchFilterMode.Mid;
            }
            else if ((bool)lastMatchRadioButton.IsChecked) {
                matchFilterMode = MatchFilterMode.Last;
            }

            List<Tuple<AudioTrack, AudioTrack>> trackPairs = 
                MatchProcessor.GetTrackPairs(trackList);
            List<Tuple<AudioTrack, AudioTrack, List<Match>>> trackPairsMatches = 
                MatchProcessor.GetTrackPairsMatches(trackPairs, multiTrackViewer.Matches);
            List<Tuple<AudioTrack, AudioTrack, List<Match>, double>> filteredTrackPairsMatches = 
                new List<Tuple<AudioTrack,AudioTrack,List<Match>, double>>();

            foreach (Tuple<AudioTrack, AudioTrack, List<Match>> trackPairMatches in trackPairsMatches) {
                List<Match> filteredMatches;

                if (trackPairMatches.Item3.Count > 0) {
                    if (matchFilterMode == MatchFilterMode.None) {
                        filteredMatches = trackPairMatches.Item3;
                    }
                    else {
                        if ((bool)windowedModeCheckBox.IsChecked) {
                            filteredMatches = MatchProcessor.WindowFilter(trackPairMatches.Item3, matchFilterMode, new TimeSpan(0, 0, int.Parse(windowSize.Text)));
                        }
                        else {
                            filteredMatches = new List<Match>();
                            filteredMatches.Add(MatchProcessor.Filter(trackPairMatches.Item3, matchFilterMode));
                        }
                    }

                    double similarity = 0;
                    foreach (Match match in filteredMatches) {
                        similarity += match.Similarity;
                    }
                    similarity /= filteredMatches.Count;

                    filteredTrackPairsMatches.Add(new Tuple<AudioTrack, AudioTrack, List<Match>, double>(
                        trackPairMatches.Item1, trackPairMatches.Item2, filteredMatches, similarity));
                }
            }

            filteredTrackPairsMatches = new List<Tuple<AudioTrack, AudioTrack, List<Match>, double>>(
                filteredTrackPairsMatches.OrderByDescending(tuple => tuple.Item4));

            List<Tuple<AudioTrack, AudioTrack, List<Match>>> filteredTrackPairs =
                new List<Tuple<AudioTrack, AudioTrack, List<Match>>>();
            foreach (Tuple<AudioTrack, AudioTrack, List<Match>, double> filteredTrackPairMatches
                in filteredTrackPairsMatches.OrderByDescending(tuple => tuple.Item4)) {
                if (filteredTrackPairs.Count < trackList.Count - 1) {
                    filteredTrackPairs.Add(new Tuple<AudioTrack, AudioTrack, List<Match>>(
                        filteredTrackPairMatches.Item1, filteredTrackPairMatches.Item2, filteredTrackPairMatches.Item3));
                    Debug.WriteLine("TrackPair {0} <-> {1}: {2} matches, similarity = {3}",
                        filteredTrackPairMatches.Item1, filteredTrackPairMatches.Item2,
                        filteredTrackPairMatches.Item3.Count, filteredTrackPairMatches.Item4);
                }
            }

            if ((bool)postProcessMatchingPointsCheckBox.IsChecked) {
                foreach (Tuple<AudioTrack, AudioTrack, List<Match>> trackPairMatches in filteredTrackPairs) {
                    MatchProcessor.ValidatePairOrder(trackPairMatches.Item3);
                    foreach (Match match in trackPairMatches.Item3) {
                        CrossCorrelation.Adjust(match, ProgressMonitor.GlobalInstance);
                    }
                }
            }

            if ((bool)removeUnusedMatchingPointsCheckBox.IsChecked) {
                multiTrackViewer.Matches.Clear();
                foreach (Tuple<AudioTrack, AudioTrack, List<Match>> trackPairMatches in filteredTrackPairs) {
                    foreach (Match match in trackPairMatches.Item3) {
                        multiTrackViewer.Matches.Add(match);
                    }
                }
            }

            MatchProcessor.AlignTracks(filteredTrackPairs);
        }

        private void crossCorrelateButton_Click(object sender, RoutedEventArgs e) {
            List<Match> matches = new List<Match>(multiTrackViewer.Matches);
            long secfactor = 1000 * 1000 * 10;

            foreach (Match matchFE in matches) {
                Match match = matchFE; // needed as reference for async task
                Task.Factory.StartNew(() => {
                    TimeSpan offset = CrossCorrelation.Calculate(match.Track1.CreateAudioStream(), new Interval(match.Track1Time.Ticks, match.Track1Time.Ticks + secfactor / 2),
                        match.Track2.CreateAudioStream(), new Interval(match.Track2Time.Ticks, match.Track2Time.Ticks + secfactor / 2), progressMonitor);

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
            fingerprintStore.FindAllMatches(3, 0.35f);
        }

        private void clearStoreButton_Click(object sender, RoutedEventArgs e) {
            fingerprintStore.Clear();
        }

        private void matchGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
            Match match = matchGrid.SelectedItem as Match;
            if(match != null) {
                TimeSpan t1 = match.Track1.Offset + match.Track1Time;
                TimeSpan t2 = match.Track2.Offset + match.Track2Time;
                TimeSpan diff = t1 - t2;
                multiTrackViewer.Display(t1 - new TimeSpan(diff.Ticks / 2), true);
            }
        }
    }
}
