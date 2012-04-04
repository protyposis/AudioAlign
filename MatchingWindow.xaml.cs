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

        private DtwPathViewer dtwPathViewer;

        public int FingerprintSize { get; set; }
        public float FingerprintBerThreshold { get; set; }
        public TimeSpan MatchFilterWindowSize { get; set; }
        public int TimeWarpFilterSize { get; set; }
        public bool TimeWarpSmoothing { get; set; }
        public bool TimeWarpInOutCue { get; set; }
        public TimeSpan TimeWarpSearchWidth { get; set; }
        public bool TimeWarpDisplay { get; set; }
        public TimeSpan CorrelationWindowSize { get; set; }
        public TimeSpan CorrelationIntervalSize { get; set; }

        public MatchingWindow(TrackList<AudioTrack> trackList, MultiTrackViewer multiTrackViewer) {
            // init non-dependency-property variables before InitializeComponent() is called
            FingerprintSize = FingerprintStore.DEFAULT_FINGERPRINT_SIZE;
            FingerprintBerThreshold = 0.45f;
            MatchFilterWindowSize = new TimeSpan(0, 0, 30);
            TimeWarpFilterSize = 100;
            TimeWarpSmoothing = true;
            TimeWarpInOutCue = true;
            TimeWarpSearchWidth = new TimeSpan(0, 0, 10);
            TimeWarpDisplay = false;
            CorrelationWindowSize = new TimeSpan(0, 0, 5);
            CorrelationIntervalSize = new TimeSpan(0, 5, 0);

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

            if (dtwPathViewer != null) {
                dtwPathViewer.Close();
            }
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

            Task.Factory.StartNew(() => Parallel.ForEach<AudioTrack>(trackList, 
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, 
                audioTrack => {
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
                    //ClearAllMatches();
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

                    TrackList<AudioTrack> alignedTracks = new TrackList<AudioTrack>();
                    TimeSpan componentStartTime = TimeSpan.Zero;
                    foreach (MatchGroup trackGroup in trackGroups) {
                        if (removeUnusedMatchingPoints) {
                            foreach (MatchPair trackPair in trackGroup.MatchPairs) {
                                foreach (Match match in trackPair.Matches) {
                                    multiTrackViewer.Matches.Add(match);
                                }
                            }
                        }

                        MatchProcessor.FilterCoincidentMatches(trackGroup.MatchPairs);
                        MatchProcessor.AlignTracks(trackGroup.MatchPairs);
                        MatchProcessor.MoveToStartTime(trackGroup.TrackList, componentStartTime);
                        alignedTracks.Add(trackGroup.TrackList);
                        componentStartTime = trackGroup.TrackList.End;
                    }

                    // process unaligned tracks (= tracks without matching points)
                    foreach (AudioTrack track in trackList.Except(alignedTracks)) {
                        track.Volume = 0;
                    }
                });
            });
        }

        private void crossCorrelateButton_Click(object sender, RoutedEventArgs e) {
            List<Match> matches = new List<Match>(matchGrid.SelectedItems.Cast<Match>());
            foreach (Match matchFE in matches) {
                Match match = matchFE; // needed as reference for async task
                Task.Factory.StartNew(() => {
                    CrossCorrelation.Adjust(match, progressMonitor);
                    Dispatcher.BeginInvoke((Action)delegate {
                        matchGrid.Items.Refresh();
                        multiTrackViewer.RefreshAdornerLayer();
                    });
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
            fingerprintStore.Threshold = FingerprintBerThreshold;
            fingerprintStore.FingerprintSize = FingerprintSize;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            List<Match> matches = fingerprintStore.FindAllMatchingMatches();
            sw.Stop();
            Debug.WriteLine(matches.Count + " matches found in {0}", sw.Elapsed);
            matches = FingerprintStore.FilterDuplicateMatches(matches);
            Debug.WriteLine(matches.Count + " matches found (filtered)");
            foreach (Match match in matches) {
                multiTrackViewer.Matches.Add(match);
            }
        }

        private void FindAllSoftMatches() {
            fingerprintStore.Threshold = FingerprintBerThreshold;
            fingerprintStore.FingerprintSize = FingerprintSize;
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
            fingerprintStore.Threshold = FingerprintBerThreshold;
            fingerprintStore.FingerprintSize = FingerprintSize;
            fingerprintStore.FindAllMatches(3, 0.35f);
        }

        private void clearStoreButton_Click(object sender, RoutedEventArgs e) {
            if (fingerprintStore != null) fingerprintStore.Clear();
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
                                calculateSimilarity, normalizeSimilarity,
                                false, false);
                        });
                    }
                }
                else {
                    List<MatchGroup> trackGroups = DetermineMatchGroups(MatchFilterMode.None);
                    Task.Factory.StartNew(() => {
                        foreach (MatchGroup trackGroup in trackGroups) {
                            foreach (MatchPair trackPair in trackGroup.MatchPairs) {
                                List<Match> matches = trackPair.Matches.OrderBy(match => match.Track1Time).ToList();
                                Match first = matches.First();
                                Match last = matches.Last();

                                //Task.Factory.StartNew(() => {
                                    TimeSpan sectionLength = first.Track1Time > first.Track2Time ? first.Track2Time : first.Track1Time;
                                    TimeWarp(type,
                                        first.Track1, first.Track1Time - sectionLength, first.Track1Time,
                                        first.Track2, first.Track2Time - sectionLength, first.Track2Time,
                                        calculateSimilarity, normalizeSimilarity,
                                        true, false);
                                //});

                                if (mode == TimeWarpMode.AllSections) {
                                    if (matches.Count > 1) {
                                        for (int i = 0; i < matches.Count - 1; i++) {
                                            Match from = matches[i];
                                            Match to = matches[i + 1];
                                            TimeWarp(type,
                                                from.Track1, from.Track1Time, to.Track1Time,
                                                from.Track2, from.Track2Time, to.Track2Time,
                                                calculateSimilarity, normalizeSimilarity,
                                                false, false);
                                        }
                                    }
                                }

                                //Task.Factory.StartNew(() => {
                                sectionLength = last.Track1.Length - last.Track1Time > last.Track2.Length - last.Track2Time ?
                                        last.Track2.Length - last.Track2Time : last.Track1.Length - last.Track1Time;
                                    TimeWarp(type,
                                        last.Track1, last.Track1Time, last.Track1Time + sectionLength,
                                        last.Track2, last.Track2Time, last.Track2Time + sectionLength,
                                        calculateSimilarity, normalizeSimilarity,
                                        false, true);
                                //});
                            }
                        }
                    });
                }
            }
        }

        private void TimeWarp(TimeWarpType type, AudioTrack t1, TimeSpan t1From, TimeSpan t1To, AudioTrack t2, TimeSpan t2From, TimeSpan t2To, bool calculateSimilarity, bool normalizeSimilarity, bool cueIn, bool cueOut) {
            IAudioStream s1 = t1.CreateAudioStream();
            IAudioStream s2 = t2.CreateAudioStream();
            s1 = new CropStream(s1, TimeUtil.TimeSpanToBytes(t1From, s1.Properties), TimeUtil.TimeSpanToBytes(t1To, s1.Properties));
            s2 = new CropStream(s2, TimeUtil.TimeSpanToBytes(t2From, s2.Properties), TimeUtil.TimeSpanToBytes(t2To, s2.Properties));

            List<Tuple<TimeSpan, TimeSpan>> path = null;
            DTW dtw = null;

            // execute time warping
            if (type == TimeWarpType.DTW) {
                dtw = new DTW(TimeWarpSearchWidth, progressMonitor);
            }
            else if (type == TimeWarpType.OLTW) {
                dtw = new OLTW2(TimeWarpSearchWidth, progressMonitor);
            }

            if (TimeWarpDisplay) {
                this.Dispatcher.BeginInvoke((Action)delegate {
                    dtwPathViewer = new DtwPathViewer();
                    dtwPathViewer.Show();
                });

                dtw.OltwInit += new DTW.OltwInitDelegate(delegate(int windowSize, IMatrix cellCostMatrix, IMatrix totalCostMatrix) {
                    dtwPathViewer.Dispatcher.BeginInvoke((Action)delegate {
                        dtwPathViewer.DtwPath.Init(windowSize, cellCostMatrix, totalCostMatrix);
                    });
                });
                bool drawing = false;
                dtw.OltwProgress += new DTW.OltwProgressDelegate(delegate(int i, int j, int minI, int minJ, bool force) {
                    if (!drawing || force) {
                        dtwPathViewer.Dispatcher.BeginInvoke((Action)delegate {
                            drawing = true;
                            dtwPathViewer.DtwPath.Refresh(i, j, minI, minJ);
                            drawing = false;
                        });
                    }
                });
            }

            path = dtw.Execute(s1, s2);

            if (path == null) {
                return;
            }

            // convert resulting path to matches and filter them
            int filterSize = TimeWarpFilterSize; // take every n-th match and drop the rest
            bool smoothing = TimeWarpSmoothing;
            int smoothingWidth = Math.Min(filterSize / 10, filterSize);
            bool inOutCue = TimeWarpInOutCue;
            TimeSpan inOutCueSpan = TimeWarpSearchWidth;
            List<Match> matches = new List<Match>();
            float maxSimilarity = 0; // needed for normalization
            IProgressReporter progressReporter = progressMonitor.BeginTask("post-process resulting path...", true);
            double totalProgress = path.Count;
            double progress = 0;

            /* Leave out matches in the in/out cue areas...
             * The matches in the interval at the beginning and end of the calculated time warping path with a width
             * equal to the search width should be left out because they might not be correct - since the time warp
             * path needs to start at (0,0) in the matrix and end at (m,n), they would only be correct if the path gets
             * calculated between two synchronization points. Paths calculated from the start of a track to the first
             * sync point, or from the last sync point to end of the track are probably wrong in this interval since
             * the start and end points don't match if there is time drift so it is better to leave them out in those
             * areas... in those short a few second long intervals the drict actually will never be that extreme that
             * someone would notice it anyway. */
            if (inOutCue) {
                int startIndex = 0;
                int endIndex = path.Count;

                // this needs a temporally ordered mapping path (no matter if ascending or descending)
                foreach(Tuple<TimeSpan, TimeSpan> mapping in path) {
                    if(cueIn && (mapping.Item1 < inOutCueSpan || mapping.Item2 < inOutCueSpan)) {
                        startIndex++;
                    }
                    if(cueOut && (mapping.Item1 > (t1To - t1From - inOutCueSpan) || mapping.Item2 > (t2To - t2From - inOutCueSpan))) {
                        endIndex--;
                    }
                }
                path = path.GetRange(startIndex, endIndex - startIndex);
            }

            for (int i = 0; i < path.Count; i += filterSize) {
                //List<Tuple<TimeSpan, TimeSpan>> section = path.GetRange(i, Math.Min(path.Count - i, filterSize));
                List<Tuple<TimeSpan, TimeSpan>> smoothingSection = path.GetRange(
                        Math.Max(0, i - smoothingWidth / 2), Math.Min(path.Count - i, smoothingWidth));
                Tuple<TimeSpan, TimeSpan> match = path[i];

                if (smoothingSection.Count == 0) {
                    throw new InvalidOperationException("must not happen");
                }
                else if (smoothingSection.Count == 1 || !smoothing || i == 0) {
                    // do nothing, match doesn't need any processing
                    // the first and last match must not be smoothed since they must sit at the bounds
                }
                else {
                    List<TimeSpan> offsets = new List<TimeSpan>(smoothingSection.Select(t => t.Item2 - t.Item1).OrderBy(t => t));
                    int middle = offsets.Count / 2;

                    // calculate median
                    // http://en.wikiversity.org/wiki/Primary_mathematics/Average,_median,_and_mode#Median
                    TimeSpan smoothedDriftTime = new TimeSpan((offsets[middle - 1] + offsets[middle]).Ticks / 2);
                    match = new Tuple<TimeSpan, TimeSpan>(match.Item1, match.Item1 + smoothedDriftTime);
                }

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

                progressReporter.ReportProgress(progress / totalProgress * 100);
                progress += filterSize;
            }

            // add last match if it hasn't been added
            if (path.Count % filterSize != 1) {
                Tuple<TimeSpan, TimeSpan> lastMatch = path[path.Count - 1];
                matches.Add(new Match() {
                    Track1 = t1,
                    Track1Time = lastMatch.Item1 + t1From,
                    Track2 = t2,
                    Track2Time = lastMatch.Item2 + t2From,
                    Similarity = 1
                });
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

        private List<MatchGroup> DetermineMatchGroups(MatchFilterMode matchFilterMode) {
            List<Match> matches = new List<Match>(multiTrackViewer.Matches);
            return MatchProcessor.DetermineMatchGroups(matchFilterMode, trackList, matches,
                (bool)windowedModeCheckBox.IsChecked, MatchFilterWindowSize);
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

            return DetermineMatchGroups(matchFilterMode);
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
                Match match = new Match {
                    Track1 = t1, Track1Time = position - t1.Offset,
                    Track2 = t2, Track2Time = position - t2.Offset,
                    Similarity = 1
                };
                multiTrackViewer.Matches.Add(match);
                matchGrid.SelectedItem = match;
                matchGrid.ScrollIntoView(match);
                addManualMatchPopup.IsOpen = false;
            }
        }

        private void correlationButton_Click(object sender, RoutedEventArgs e) {
            List<MatchGroup> trackGroups = DetermineMatchGroups();
            foreach (MatchGroup trackGroup in trackGroups) {
                foreach (MatchPair trackPair in trackGroup.MatchPairs) {
                    MatchPair localMP = trackPair;
                    Task.Factory.StartNew(() => {
                        TimeSpan t1Offset;
                        TimeSpan t2Offset;
                        if (localMP.Track1.Offset < localMP.Track2.Offset) {
                            t1Offset = localMP.Track2.Offset - localMP.Track1.Offset;
                            t2Offset = TimeSpan.Zero;
                        }
                        else {
                            t1Offset = TimeSpan.Zero;
                            t2Offset = localMP.Track1.Offset - localMP.Track2.Offset;
                        }
                        TimeSpan length;
                        if (localMP.Track1.Length > localMP.Track2.Length) {
                            length = localMP.Track2.Length;
                        }
                        else {
                            length = localMP.Track1.Length;
                        }
                        TimeSpan interval = CorrelationIntervalSize;
                        TimeSpan window = CorrelationWindowSize;

                        List<Match> computedMatches = new List<Match>();
                        for (TimeSpan position = TimeSpan.Zero; position < length; position += interval) {
                            Interval t1Interval = new Interval((t1Offset + position).Ticks, (t1Offset + position + window).Ticks);
                            Interval t2Interval = new Interval((t2Offset + position).Ticks, (t2Offset + position + window).Ticks);

                            if (t1Interval.TimeTo >= localMP.Track1.Length || t2Interval.TimeTo >= localMP.Track2.Length) {
                                // not enough samples remaining to compute the correlation (end of track reached)
                                break;
                            }

                            double maxVal;
                            TimeSpan offset = CrossCorrelation.Calculate(
                                localMP.Track1.CreateAudioStream(), t1Interval,
                                localMP.Track2.CreateAudioStream(), t2Interval,
                                progressMonitor, out maxVal);
                            // always apply a positive offset that moves the match position inside the corelation interval,
                            // else it can happen that a negative offset is applied to a match at the beginning of the stream
                            // which means that the matching point would be at a negative position in the audio stream
                            computedMatches.Add(new Match {
                                Track1 = localMP.Track1, Track1Time = t1Offset + position + (offset < TimeSpan.Zero ? -offset : TimeSpan.Zero),
                                Track2 = localMP.Track2, Track2Time = t2Offset + position + (offset >= TimeSpan.Zero ? offset : TimeSpan.Zero),
                                Similarity = (float)maxVal
                            });
                        }

                        Dispatcher.BeginInvoke((Action)delegate {
                            foreach (Match match in computedMatches) {
                                multiTrackViewer.Matches.Add(match);
                            }
                        });
                    });
                }
            }
        }
    }
}
