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
using AudioAlign.Audio.Matching.Graph;
using AudioAlign.Audio.Matching.Dixon2005;
using AudioAlign.Audio.Streams;

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
            dtwRadioButton.IsChecked = true;

            matchGrid.ItemsSource = multiTrackViewer.Matches;
            profileComboBox.ItemsSource = FingerprintGenerator.GetProfiles();
            profileComboBox.SelectedIndex = 0;
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
            IProfile profile = (IProfile)profileComboBox.SelectedItem;
            fingerprintStore = new FingerprintStore(profile);

            Task.Factory.StartNew(() => Parallel.ForEach<AudioTrack>(trackList, audioTrack => {
                DateTime startTime = DateTime.Now;
                IProgressReporter progressReporter = progressMonitor.BeginTask("Generating sub-fingerprints for " + audioTrack.FileInfo.Name, true);

                FingerprintGenerator fpg = new FingerprintGenerator(profile, audioTrack, 3, true);
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
            bool postProcessMatchingPoints = (bool)postProcessMatchingPointsCheckBox.IsChecked;
            bool removeUnusedMatchingPoints = (bool)removeUnusedMatchingPointsCheckBox.IsChecked;

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

            List<Match> matches = new List<Match>(multiTrackViewer.Matches);
            List<MatchGroup> trackGroups = DetermineMatchGroups(matchFilterMode, trackList, matches,
                (bool)windowedModeCheckBox.IsChecked, new TimeSpan(0, 0, int.Parse(windowSize.Text)));

            Task.Factory.StartNew(() => {
                Parallel.ForEach(trackGroups, trackGroup => {
                    if (postProcessMatchingPoints) {
                        Parallel.ForEach(trackGroup.MatchPairs, trackPair => {
                            MatchProcessor.ValidatePairOrder(trackPair.Matches);
                            foreach (Match match in trackPair.Matches) {
                                CrossCorrelation.Adjust(match, progressMonitor);
                            }
                        });
                    }
                });

                Dispatcher.BeginInvoke((Action)delegate {
                    if (removeUnusedMatchingPoints) {
                        multiTrackViewer.Matches.Clear();
                    }

                    TimeSpan componentStartTime = TimeSpan.Zero;
                    foreach (MatchGroup trackGroup in trackGroups) {
                        if (removeUnusedMatchingPoints) {
                            foreach (MatchPair trackPair in trackGroup.MatchPairs) {
                                foreach (Match match in trackPair.Matches) {
                                    multiTrackViewer.Matches.Add(match);
                                }
                            }
                        }

                        MatchProcessor.AlignTracks(trackGroup.MatchPairs);
                        MatchProcessor.MoveToStartTime(trackGroup.TrackList, componentStartTime);
                        componentStartTime = trackGroup.TrackList.End;
                    }
                });
            });
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

        private void dtwButton_Click(object sender, RoutedEventArgs e) {
            if (trackList.Count > 1) {
                int mode = 0;
                if ((bool)dtwRadioButton.IsChecked) {
                    mode = 1;
                }
                else if ((bool)oltwRadioButton.IsChecked) {
                    mode = 2;
                }
                bool calculateSimilarity = (bool)dtwSimilarityCheckBox.IsChecked;
                bool normalizeSimilarity = (bool)dtwSimilarityNormalizationCheckBox.IsChecked;

                Task.Factory.StartNew(() => {
                    IAudioStream s1 = trackList[0].CreateAudioStream();
                    IAudioStream s2 = trackList[1].CreateAudioStream();

                    List<Tuple<TimeSpan, TimeSpan>> path = null;

                    // execute time warping
                    if (mode == 1) {
                        DTW dtw = new DTW(new TimeSpan(0, 0, 10), progressMonitor);
                        path = dtw.Execute(s1, s2);
                    }
                    else if (mode == 2) {
                        OLTW oltw = new OLTW(progressMonitor);
                        path = oltw.Execute(s1, s2);
                    }

                    if (path == null) {
                        return;
                    }

                    // convert resulting path to matches and filter them
                    int filterSize = 10; // take every n-th match and drop the rest
                    int count = 0;
                    List<Match> matches = new List<Match>();
                    float maxSimilarity = 0; // needed for normalization
                    IProgressReporter progressReporter = progressMonitor.BeginTask("post-process resulting path...", true);
                    double totalProgress = path.Count;
                    double progress = 0;
                    foreach (Tuple<TimeSpan, TimeSpan> match in path) {
                        if (count++ >= filterSize) {
                            float similarity = calculateSimilarity ? (float)Math.Abs(CrossCorrelation.Correlate(
                                s1, new Interval(match.Item1.Ticks, match.Item1.Ticks + TimeUtil.SECS_TO_TICKS),
                                s2, new Interval(match.Item2.Ticks, match.Item2.Ticks + TimeUtil.SECS_TO_TICKS))) : 1;

                            if (similarity > maxSimilarity) {
                                maxSimilarity = similarity;
                            }

                            matches.Add(new Match() {
                                Track1 = trackList[0],
                                Track1Time = match.Item1,
                                Track2 = trackList[1],
                                Track2Time = match.Item2,
                                Similarity = similarity
                            });
                            count = 0;
                            progressReporter.ReportProgress(progress / totalProgress * 100);
                        }
                        progress++;
                    }
                    progressReporter.Finish();

                    multiTrackViewer.Dispatcher.BeginInvoke((Action)delegate {
                        foreach (Match match in matches) {
                            if (normalizeSimilarity) {
                                match.Similarity /= maxSimilarity; // normalize to 1
                            }
                            multiTrackViewer.Matches.Add(match);
                        }
                    });
                });
            }
        }

        private List<MatchGroup> DetermineMatchGroups(MatchFilterMode matchFilterMode, TrackList<AudioTrack> trackList, 
                                                      List<Match> matches, bool windowed, TimeSpan windowSize) {
            List<MatchPair> trackPairs = MatchProcessor.GetTrackPairs(trackList);
            MatchProcessor.AssignMatches(trackPairs, matches);
            trackPairs = trackPairs.Where(matchPair => matchPair.Matches.Count > 0).ToList(); // remove all track pairs without matches

            // filter matches
            foreach (MatchPair trackPair in trackPairs) {
                List<Match> filteredMatches;

                if (trackPair.Matches.Count > 0) {
                    if (matchFilterMode == MatchFilterMode.None) {
                        filteredMatches = trackPair.Matches;
                    }
                    else {
                        if (windowed) {
                            filteredMatches = MatchProcessor.WindowFilter(trackPair.Matches, matchFilterMode, windowSize);
                        }
                        else {
                            filteredMatches = new List<Match>();
                            filteredMatches.Add(MatchProcessor.Filter(trackPair.Matches, matchFilterMode));
                        }
                    }

                    trackPair.Matches = filteredMatches;
                }
            }

            // determine connected tracks
            UndirectedGraph<AudioTrack, double> trackGraph = new UndirectedGraph<AudioTrack, double>();
            foreach (MatchPair trackPair in trackPairs) {
                trackGraph.Add(new Edge<AudioTrack, double>(trackPair.Track1, trackPair.Track2, 1d - trackPair.CalculateAverageSimilarity()) {
                    Tag = trackPair
                });
            }

            List<UndirectedGraph<AudioTrack, double>> trackGraphComponents = trackGraph.GetConnectedComponents();
            Debug.WriteLine("{0} disconnected components", trackGraphComponents.Count);

            List<MatchGroup> trackGroups = new List<MatchGroup>();
            foreach (UndirectedGraph<AudioTrack, double> component in trackGraphComponents) {
                List<MatchPair> connectedTrackPairs = new List<MatchPair>();

                foreach (Edge<AudioTrack, double> edge in component.GetMinimalSpanningTree().Edges) {
                    connectedTrackPairs.Add((MatchPair)edge.Tag);
                }

                foreach (MatchPair filteredTrackPair in connectedTrackPairs) {
                    Debug.WriteLine("TrackPair {0} <-> {1}: {2} matches, similarity = {3}",
                        filteredTrackPair.Track1, filteredTrackPair.Track2,
                        filteredTrackPair.Matches.Count, filteredTrackPair.CalculateAverageSimilarity());
                }

                TrackList<AudioTrack> componentTrackList = new TrackList<AudioTrack>(component.Vertices);

                trackGroups.Add(new MatchGroup {
                    MatchPairs = connectedTrackPairs,
                    TrackList = componentTrackList
                });
            }

            return trackGroups;
        }
    }
}
