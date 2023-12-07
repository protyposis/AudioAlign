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
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Aurio.Project;
using Match = Aurio.Matching.Match;

namespace AudioAlign.ViewModels
{
    public class JikuToolsViewModel
    {
        private readonly DelegateCommand moveCommand;
        private readonly DelegateCommand timestampAlignCommand;
        private readonly DelegateCommand driftCorrectCommand;
        private readonly DelegateCommand evaluateOffsetsCommand;

        public JikuToolsViewModel(
            TrackList<AudioTrack> trackList,
            Collection<Match> matchCollection
        )
        {
            moveCommand = new DelegateCommand(o =>
            {
                try
                {
                    JikuDatasetUtils.Move(trackList, SelectionRegex, MovementOffset);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("moving failed:");
                    Console.WriteLine(ex);
                }
            });
            timestampAlignCommand = new DelegateCommand(o =>
            {
                JikuDatasetUtils.TimestampAlign(trackList, SelectionRegex);
            });
            driftCorrectCommand = new DelegateCommand(o =>
            {
                DriftCorrect(matchCollection);
            });
            evaluateOffsetsCommand = new DelegateCommand(o =>
            {
                JikuDatasetUtils.EvaluateOffsets(trackList);
            });

            SelectionRegex = ".*";
        }

        public string SelectionRegex { get; set; }
        public TimeSpan MovementOffset { get; set; }

        /// <summary>
        /// Reads drift factors from a config file and applies them to all matches by scaling
        /// their 2 match positions by the drift factor.
        /// </summary>
        private static void DriftCorrect(Collection<Match> matchCollection)
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    DefaultExt = ".txt",
                    Filter = "Drift Config|*.txt",
                    Multiselect = false
                };

                if (dialog.ShowDialog() == true)
                {
                    // read drift config
                    Dictionary<string, double> mapping = new();
                    using (StreamReader reader = File.OpenText(dialog.FileName))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            string[] parts = line.Split(';');
                            mapping.Add(parts[0], Double.Parse(parts[1]));
                        }
                    }

                    // adjust matching points
                    foreach (Match m in matchCollection)
                    {
                        foreach (string filePattern in mapping.Keys)
                        {
                            double factor = mapping[filePattern];
                            string regex =
                                "^" + filePattern.Replace(".", "\\.").Replace("*", ".+") + "$";

                            if (Regex.IsMatch(m.Track1.FileInfo.Name, regex))
                            {
                                m.Track1Time = new TimeSpan((long)(m.Track1Time.Ticks * factor));
                                Debug.WriteLine("adjusted " + m.Track1.Name);
                            }
                            if (Regex.IsMatch(m.Track2.FileInfo.Name, regex))
                            {
                                m.Track2Time = new TimeSpan((long)(m.Track2Time.Ticks * factor));
                                Debug.WriteLine("adjusted " + m.Track2.Name);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        public DelegateCommand MoveCommand
        {
            get { return moveCommand; }
        }
        public DelegateCommand TimestampAlignCommand
        {
            get { return timestampAlignCommand; }
        }
        public DelegateCommand DriftCorrectCommand
        {
            get { return driftCorrectCommand; }
        }
        public DelegateCommand EvaluateOffsetsCommand
        {
            get { return evaluateOffsetsCommand; }
        }
    }
}
