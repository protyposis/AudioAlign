using Aurio.Audio;
using Aurio.Audio.Matching;
using Aurio.Audio.Matching.Echoprint;
using Aurio.Audio.Project;
using Aurio.Audio.TaskMonitor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AudioAlign.Models {
    public class EchoprintFingerprintingModel {

        private Profile[] profiles;
        private FingerprintStore store;

        public event EventHandler FingerprintingFinished;

        public EchoprintFingerprintingModel() {
            profiles = FingerprintGenerator.GetProfiles();
            Reset(profiles[0]);
        }

        public Profile[] Profiles {
            get { return profiles; }
        }

        public Profile SelectedProfile { get; set; }

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
                    var progressReporter = progressMonitor.BeginTask("Generating fingerprint hashes for " + track.FileInfo.Name, true);
                    var generator = new FingerprintGenerator(SelectedProfile);
                    int hashesCalculated = 0;

                    generator.SubFingerprintsGenerated += delegate(object s, SubFingerprintsGeneratedEventArgs e) {
                        hashesCalculated += e.SubFingerprints.Count;
                        progressReporter.ReportProgress((double)e.Index / e.Indices * 100);
                        store.Add(e);
                    };

                    generator.Generate(track);

                    progressReporter.Finish();
                    Debug.WriteLine("Fingerprint hash generation finished with " + hashesCalculated + " hashes in " + (DateTime.Now - startTime));

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

                EventHandler<ValueEventArgs<double>> progressHandler = delegate(object sender, ValueEventArgs<double> e) {
                    progressReporter.ReportProgress(e.Value);
                };

                Stopwatch sw = new Stopwatch();
                sw.Start();
                store.MatchingProgress += progressHandler;
                matches = store.FindAllMatches();
                store.MatchingProgress -= progressHandler;
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
