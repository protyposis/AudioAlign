using AudioAlign.Properties;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace AudioAlign {
    public class RecentProjects {

        public const string ClearCommand = "clear";
        public const int MaxEntries = 10;

        private ObservableCollection<RecentEntry> observableRecentProjects;
        private StringCollection recentProjects;

        public RecentProjects() {
            observableRecentProjects = new ObservableCollection<RecentEntry>();
        }

        public void Load() {
            recentProjects = Settings.Default.RecentProjects;

            if (recentProjects == null) {
                recentProjects = new StringCollection();
            }
            
            observableRecentProjects.Clear();
            if (recentProjects.Count == 0) {
                AddEntryNoRecent();
            }
            else {
                foreach(string entry in recentProjects) {
                    observableRecentProjects.Add(new RecentEntry {
                        Title = entry,
                        Enabled = true,
                        Parameter = entry
                    });
                }
                AddEntryClearRecent();
            }
        }

        public void Save() {
            Settings.Default.RecentProjects = recentProjects;
            Settings.Default.Save();
        }

        public void Clear() {
            recentProjects.Clear();
            Save();

            observableRecentProjects.Clear();
            AddEntryNoRecent();
        }

        public int Count {
            get { return recentProjects.Count; }
        }

        public void Add(string entry) {
            bool empty = (Count == 0);
            int oldIndex = -1;

            // Remove entry if it is already somewhere in the list
            if (!empty) {
                oldIndex = recentProjects.IndexOf(entry);
                recentProjects.Remove(entry);
            }

            // (Re)Add entry at the top
            recentProjects.Insert(0, entry);

            if (oldIndex == -1 && recentProjects.Count > MaxEntries) {
                // Limit list to max number of entries
                recentProjects.RemoveAt(MaxEntries);
            }

            // Add menu entry
            if (empty) {
                // remove NoRecent menu entry
                observableRecentProjects.Clear();
            }
            else if(oldIndex > -1) {
                // remove exisiting menu entry
                observableRecentProjects.RemoveAt(oldIndex);
            }
            // (Re)Add menu entry at the top
            observableRecentProjects.Insert(0, new RecentEntry {
                Title = entry,
                Enabled = true,
                Parameter = entry
            });
            if (oldIndex == -1 && observableRecentProjects.Count > MaxEntries + 1) {
                // Limit list to max number of entries
                observableRecentProjects.RemoveAt(MaxEntries);
            }

            if (empty) {
                // Add clear recent entry when the first item is added
                AddEntryClearRecent();
            }
        }

        /// <remarks>This collection is only for reading. Do not modify collection from outside!</remarks>
        public StringCollection Entries {
            get { return recentProjects; }
        }

        /// <remarks>This collection is only for reading. Do not modify collection from outside!</remarks>
        public ObservableCollection<RecentEntry> MenuEntries {
            get { return observableRecentProjects; }
        }

        private void AddEntryNoRecent() {
            observableRecentProjects.Add(new RecentEntry {
                Title = "No recent projects",
                Enabled = false
            });
        }

        private void AddEntryClearRecent() {
            observableRecentProjects.Add(new RecentEntry {
                Title = "Clear recent projects list",
                Enabled = true,
                Parameter = ClearCommand
            });
        }

        public class RecentEntry {
            public string Title { get; set; }
            public bool Enabled { get; set; }
            public string Parameter { get; set; }
        }
    }
}
