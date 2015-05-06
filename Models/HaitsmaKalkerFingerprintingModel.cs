using AudioAlign.Audio;
using AudioAlign.Audio.Matching;
using AudioAlign.Audio.Matching.HaitsmaKalker2002;
using AudioAlign.Audio.Project;
using AudioAlign.Audio.TaskMonitor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AudioAlign.Models {
    public class HaitsmaKalkerFingerprintingModel {

        private Profile[] profiles;
        private FingerprintStore store;

        public event EventHandler FingerprintingFinished;

        public HaitsmaKalkerFingerprintingModel() {
            FingerprintBerThreshold = 0.45f;
            FingerprintSize = FingerprintStore.DEFAULT_FINGERPRINT_SIZE;
            profiles = FingerprintGenerator.GetProfiles();
            SelectedProfile = profiles[0];
            Reset();
        }

        public Profile[] Profiles {
            get { return profiles; }
        }

        public Profile SelectedProfile { get; set; }

        public float FingerprintBerThreshold { get; set; }

        public int FingerprintSize { get; set; }

        /// <summary>
        /// Resets the model by clearing all data and configuring it with a new profile.
        /// </summary>
        /// <param name="profile">the new profile to configure the model with</param>
        public void Reset(Profile profile) {
            if (profile == null) {
                throw new ArgumentNullException("profile must not be null");
            }

            SelectedProfile = profile;
            store = new FingerprintStore(profile);
        }

        /// <summary>
        /// Resets the model by clearing all data and configuring it with the current profile.
        /// </summary>
        public void Reset() {
            Reset(SelectedProfile);
        }

        public void Fingerprint(List<AudioTrack> tracks, ProgressMonitor progressMonitor) {
            var selfReference = this;
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            long startMemory = GC.GetTotalMemory(false);

            Task.Factory.StartNew(() => Parallel.ForEach<AudioTrack>(tracks,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                track => {
                    var startTime = DateTime.Now;
                    var progressReporter = progressMonitor.BeginTask("Generating sub-fingerprints for " + track.FileInfo.Name, true);
                    var generator = new FingerprintGenerator(SelectedProfile, track, 3);
                    int subFingerprintsCalculated = 0;

                    generator.SubFingerprintsGenerated += new EventHandler<SubFingerprintsGeneratedEventArgs>(delegate(object s2, SubFingerprintsGeneratedEventArgs e2) {
                        subFingerprintsCalculated += e2.SubFingerprints.Count;
                        progressReporter.ReportProgress((double)e2.Index / e2.Indices * 100);
                        store.Add(e2);
                    });

                    generator.Generate();

                    progressReporter.Finish();
                    Debug.WriteLine("subfingerprint generation finished with " + subFingerprintsCalculated + " hashes in " + (DateTime.Now - startTime));

                }))
                .ContinueWith(task => {
                    // all running generator tasks have finished
                    stopwatch.Stop();
                    long memory = GC.GetTotalMemory(false) - startMemory;
                    Debug.WriteLine("Fingerprinting finished in " + stopwatch.Elapsed + " (mem: " + (memory / 1024 / 1024) + " MB)");
                    
                    if (FingerprintingFinished != null) {
                        FingerprintingFinished(selfReference, EventArgs.Empty);
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public List<Match> FindAllMatches(ProgressMonitor progressMonitor, Action<List<Match>> callback) {
            List<Match> matches = null;

            // NOTE The following task is passed the "default" task scheduler, because otherwise
            //      it uses the "current" scheduler, which can be the UI scheduler when called
            //      from a task run by the TaskScheduler.FromCurrentSynchronizationContext(), leading
            //      to a blocked UI.

            Task.Factory.StartNew(() => {
                var progressReporter = progressMonitor.BeginTask("Matching hashes...", true);

                Stopwatch sw = new Stopwatch();
                sw.Start();
                store.Threshold = FingerprintBerThreshold;
                store.FingerprintSize = FingerprintSize;
                matches = store.FindAllMatches();
                sw.Stop();
                Debug.WriteLine(matches.Count + " matches found in {0}", sw.Elapsed);

                matches = MatchProcessor.FilterDuplicateMatches(matches);
                Debug.WriteLine(matches.Count + " matches found (filtered)");

                progressReporter.Finish();
            }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default) // Use default scheduler, see NOTE above
            .ContinueWith(task => {
                callback.Invoke(matches);
            }, TaskScheduler.FromCurrentSynchronizationContext());
            
            return matches;
        }
    }
}
