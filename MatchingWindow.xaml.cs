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

        public TimeSpan MatchFilterWindowSize { get; set; }
        public int TimeWarpFilterSize { get; set; }
        public TimeSpan TimeWarpSearchWidth { get; set; }

        public MatchingWindow(TrackList<AudioTrack> trackList, MultiTrackViewer multiTrackViewer) {
            // init non-dependency-property variables before InitializeComponent() is called
            MatchFilterWindowSize = new TimeSpan(0, 0, 30);
            TimeWarpFilterSize = 100;
            TimeWarpSearchWidth = new TimeSpan(0, 0, 10);

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
            timeWarpModeBorderSectionsRadioButton.IsChecked = true;

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

        private void filterMatchesButton_Click(object sender, RoutedEventArgs e) {
            List<MatchGroup> trackGroups = DetermineMatchGroups();
            multiTrackViewer.Matches.Clear();
            foreach (MatchGroup trackGroup in trackGroups) {
                foreach (MatchPair trackPair in trackGroup.MatchPairs) {
                    foreach (Match match in trackPair.Matches) {
                        multiTrackViewer.Matches.Add(match);
                    }
                }
            }
        }

        private void alignTracksButton_Click(object sender, RoutedEventArgs e) {
            bool postProcessMatchingPoints = (bool)postProcessMatchingPointsCheckBox.IsChecked;
            bool removeUnusedMatchingPoints = (bool)removeUnusedMatchingPointsCheckBox.IsChecked;

            List<Match> matches = new List<Match>(multiTrackViewer.Matches);
            List<MatchGroup> trackGroups = DetermineMatchGroups();

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
            if (match != null) {
                TimeSpan t1 = match.Track1.Offset + match.Track1Time;
                TimeSpan t2 = match.Track2.Offset + match.Track2Time;
                TimeSpan diff = t1 - t2;
                multiTrackViewer.Display(t1 - new TimeSpan(diff.Ticks / 2), true);
            }
        }

        private void dtwButton_Click(object sender, RoutedEventArgs e) {
            if (trackList.Count > 1) {
                TimeWarpType type = TimeWarpType.DTW;
                if ((bool)dtwRadioButton.IsChecked) {
                    type = TimeWarpType.DTW;
                }
                else if ((bool)oltwRadioButton.IsChecked) {
                    type = TimeWarpType.OLTW;
                }

                TimeWarpMode mode = TimeWarpMode.FirstTwoTracks;
                if ((bool)timeWarpModeBorderSectionsRadioButton.IsChecked) {
                    mode = TimeWarpMode.BorderSections;
                }
                else if ((bool)timeWarpModeAllSectionsRadioButton.IsChecked) {
                    mode = TimeWarpMode.AllSections;
                }

                bool calculateSimilarity = (bool)dtwSimilarityCheckBox.IsChecked;
                bool normalizeSimilarity = (bool)dtwSimilarityNormalizationCheckBox.IsChecked;

                if (mode == TimeWarpMode.FirstTwoTracks) {
                    if (trackList.Count > 1) {
                        Task.Factory.StartNew(() => {
                            TimeWarp(type,
                                trackList[0], TimeSpan.Zero, trackList[0].Length,
                                trackList[1], TimeSpan.Zero, trackList[1].Length,
                                calculateSimilarity, normalizeSimilarity);
                        });
                    }
                }
                else {
                    List<MatchGroup> trackGroups = DetermineMatchGroups();
                    foreach (MatchGroup trackGroup in trackGroups) {
                        foreach (MatchPair trackPair in trackGroup.MatchPairs) {
                            List<Match> matches = trackPair.Matches.OrderBy(match => { return match.Track1Time; }).ToList();
                            Match first = matches.First();
                            Match last = matches.Last();

                            Task.Factory.StartNew(() => {
                                TimeSpan sectionLength = first.Track1Time > first.Track2Time ? first.Track2Time : first.Track1Time;
                                TimeWarp(type,
                                    first.Track1, first.Track1Time - sectionLength, first.Track1Time,
                                    first.Track2, first.Track2Time - sectionLength, first.Track2Time,
                                    calculateSimilarity, normalizeSimilarity);
                            });

                            if (mode == TimeWarpMode.AllSections) {
                                if (matches.Count > 1) {
                                    for (int i = 0; i < matches.Count - 1; i++) {
                                        Match from = matches[i];
                                        Match to = matches[i + 1];
                                        TimeWarp(type,
                                            from.Track1, from.Track1Time, to.Track1Time,
                                            from.Track2, from.Track2Time, to.Track2Time,
                                            calculateSimilarity, normalizeSimilarity);
                                    }
                                }
                            }

                            Task.Factory.StartNew(() => {
                                TimeSpan sectionLength = first.Track1.Length - first.Track1Time > first.Track2.Length - first.Track2Time ?
                                    first.Track2.Length - first.Track2Time : first.Track1.Length - first.Track1Time;
                                TimeWarp(type,
                                    first.Track1, first.Track1Time, first.Track1Time + sectionLength,
                                    first.Track2, first.Track2Time, first.Track2Time + sectionLength,
                                    calculateSimilarity, normalizeSimilarity);
                            });
                        }
                    }
                }
            }
        }

        private void TimeWarp(TimeWarpType type, AudioTrack t1, TimeSpan t1From, TimeSpan t1To, AudioTrack t2, TimeSpan t2From, TimeSpan t2To, bool calculateSimilarity, bool normalizeSimilarity) {
            IAudioStream s1 = t1.CreateAudioStream();
            IAudioStream s2 = t2.CreateAudioStream();
            s1 = new CropStream(s1, TimeUtil.TimeSpanToBytes(t1From, s1.Properties), TimeUtil.TimeSpanToBytes(t1To, s1.Properties));
            s2 = new CropStream(s2, TimeUtil.TimeSpanToBytes(t2From, s2.Properties), TimeUtil.TimeSpanToBytes(t2To, s2.Properties));

            List<Tuple<TimeSpan, TimeSpan>> path = null;

            // execute time warping
            if (type == TimeWarpType.DTW) {
                DTW dtw = new DTW(TimeWarpSearchWidth, progressMonitor);
                path = dtw.Execute(s1, s2);
            }
            else if (type == TimeWarpType.OLTW) {
                OLTW oltw = new OLTW(TimeWarpSearchWidth, progressMonitor);
                path = oltw.Execute(s1, s2);
            }

            if (path == null) {
                return;
            }

            // convert resulting path to matches and filter them
            int filterSize = TimeWarpFilterSize; // take every n-th match and drop the rest
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
                        Track1 = t1,
                        Track1Time = match.Item1 + t1From,
                        Track2 = t2,
                        Track2Time = match.Item2 + t2From,
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
        }

        private List<MatchGroup> DetermineMatchGroups() {
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

            List<Match> matches = new List<Match>(multiTrackViewer.Matches);
            return DetermineMatchGroups(matchFilterMode, trackList, matches,
                (bool)windowedModeCheckBox.IsChecked, MatchFilterWindowSize);
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

        private void addManualMatchButton_Click(object sender, RoutedEventArgs e) {
            TimeSpan position = new TimeSpan(multiTrackViewer.VirtualCaretOffset);

            // remove selections to force update of the displayed text
            addManualMatchPopupComboBoxA.SelectedItem = null;
            addManualMatchPopupComboBoxB.SelectedItem = null;

            addManualMatchPopupComboBoxA.ItemsSource = trackList.EnumerateAtPosition(position);
            addManualMatchPopupComboBoxB.ItemsSource = trackList.EnumerateAtPosition(position);

            // preselection of first two tracks
            // TODO make preselection more intelligent (don't preselect tracks not visible in the viewport, etc...)
            if (addManualMatchPopupComboBoxA.Items.Count > 0) {
                addManualMatchPopupComboBoxA.SelectedIndex = 0;
                addManualMatchPopupComboBoxB.SelectedIndex = 1;
                if (addManualMatchPopupComboBoxB.Items.Count > 1) {
                    addManualMatchPopupComboBoxB.SelectedIndex = 1;
                }
            }

            addManualMatchPopup.IsOpen = true;
        }

        private void addManualMatchPopupAddButton_Click(object sender, RoutedEventArgs e) {
            TimeSpan position = new TimeSpan(multiTrackViewer.VirtualCaretOffset);
            AudioTrack t1 = addManualMatchPopupComboBoxA.SelectedItem as AudioTrack;
            AudioTrack t2 = addManualMatchPopupComboBoxB.SelectedItem as AudioTrack;
            if (t1 != null && t2 != null) {
                multiTrackViewer.Matches.Add(new Match {
                    Track1 = t1, Track1Time = position - t1.Offset,
                    Track2 = t2, Track2Time = position - t2.Offset,
                    Similarity = 1
                });
            }
        }
    }
}
