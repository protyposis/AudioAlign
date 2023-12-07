//
// AudioAlign: Audio Synchronization and Analysis Tool
// Copyright (C) 2010-2015  Mario Guggenberger <mg@protyposis.net>
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
//
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using AudioAlign.Properties;

namespace AudioAlign
{
    public class RecentProjects
    {
        public const string ClearCommand = "clear";
        public const int MaxEntries = 10;

        private ObservableCollection<RecentEntry> observableRecentProjects;
        private StringCollection recentProjects;

        public RecentProjects()
        {
            observableRecentProjects = new ObservableCollection<RecentEntry>();
        }

        public void Load()
        {
            recentProjects = Settings.Default.RecentProjects ?? new StringCollection();

            observableRecentProjects.Clear();
            if (recentProjects.Count == 0)
            {
                AddEntryNoRecent();
            }
            else
            {
                foreach (string entry in recentProjects)
                {
                    observableRecentProjects.Add(
                        new RecentEntry
                        {
                            Title = entry,
                            Enabled = true,
                            Parameter = entry
                        }
                    );
                }
                AddEntryClearRecent();
            }
        }

        public void Save()
        {
            Settings.Default.RecentProjects = recentProjects;
            Settings.Default.Save();
        }

        public void Clear()
        {
            recentProjects.Clear();
            Save();

            observableRecentProjects.Clear();
            AddEntryNoRecent();
        }

        public int Count
        {
            get { return recentProjects.Count; }
        }

        public void Add(string entry)
        {
            bool empty = (Count == 0);
            int oldIndex = -1;

            // Remove entry if it is already somewhere in the list
            if (!empty)
            {
                oldIndex = recentProjects.IndexOf(entry);
                recentProjects.Remove(entry);
            }

            // (Re)Add entry at the top
            recentProjects.Insert(0, entry);

            if (oldIndex == -1 && recentProjects.Count > MaxEntries)
            {
                // Limit list to max number of entries
                recentProjects.RemoveAt(MaxEntries);
            }

            // Add menu entry
            if (empty)
            {
                // remove NoRecent menu entry
                observableRecentProjects.Clear();
            }
            else if (oldIndex > -1)
            {
                // remove exisiting menu entry
                observableRecentProjects.RemoveAt(oldIndex);
            }
            // (Re)Add menu entry at the top
            observableRecentProjects.Insert(
                0,
                new RecentEntry
                {
                    Title = entry,
                    Enabled = true,
                    Parameter = entry
                }
            );
            if (oldIndex == -1 && observableRecentProjects.Count > MaxEntries + 1)
            {
                // Limit list to max number of entries
                observableRecentProjects.RemoveAt(MaxEntries);
            }

            if (empty)
            {
                // Add clear recent entry when the first item is added
                AddEntryClearRecent();
            }
        }

        /// <remarks>This collection is only for reading. Do not modify collection from outside!</remarks>
        public StringCollection Entries
        {
            get { return recentProjects; }
        }

        /// <remarks>This collection is only for reading. Do not modify collection from outside!</remarks>
        public ObservableCollection<RecentEntry> MenuEntries
        {
            get { return observableRecentProjects; }
        }

        private void AddEntryNoRecent()
        {
            observableRecentProjects.Add(
                new RecentEntry { Title = "No recent projects", Enabled = false }
            );
        }

        private void AddEntryClearRecent()
        {
            observableRecentProjects.Add(
                new RecentEntry
                {
                    Title = "Clear recent projects list",
                    Enabled = true,
                    Parameter = ClearCommand
                }
            );
        }

        public class RecentEntry
        {
            public string Title { get; set; }
            public bool Enabled { get; set; }
            public string Parameter { get; set; }
        }
    }
}
