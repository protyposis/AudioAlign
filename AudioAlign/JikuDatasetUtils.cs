using Aurio;
using Aurio.Project;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AudioAlign
{
    class JikuDatasetUtils
    {
        /// <summary>
        /// Reads timestamps from the Jiku file names and aligns the tracks according to these timestamps on the timeline.
        /// </summary>
        /// <param name="trackList"></param>
        /// <param name="tracknameRegexFilterPattern">regex to restrict alignment to matching tracks</param>
        public static void TimestampAlign(TrackList<AudioTrack> trackList, string tracknameRegexFilterPattern)
        {
            Dictionary<Track, long> offsets = new Dictionary<Track, long>(trackList.Count);
            long minOffset = long.MaxValue;
            string pattern = tracknameRegexFilterPattern;
            bool patternSelect = !(String.IsNullOrEmpty(pattern) || pattern.Trim().Equals("*"));

            foreach (Track t in trackList)
            {
                if (t.Locked || (patternSelect && !Regex.IsMatch(t.Name, pattern))) continue;
                System.Text.RegularExpressions.Match m = Regex.Match(t.Name, "[0-9]{10,}");
                if (m.Success)
                {
                    long offset = long.Parse(m.Value);
                    minOffset = Math.Min(minOffset, offset);
                    offsets.Add(t, offset);
                }
            }

            foreach (Track t in offsets.Keys)
            {
                t.Offset = new TimeSpan((offsets[t] - minOffset) * TimeUtil.MILLISECS_TO_TICKS);
            }
        }

        /// <summary>
        /// Moves all tracks whose names (usually these are the file names) match a regex pattern by the specified time in the timeline.
        /// </summary>
        /// <param name="trackList"></param>
        /// <param name="tracknameRegexFilterPattern"></param>
        /// <param name="moveTime"></param>
        public static void Move(TrackList<AudioTrack> trackList, string tracknameRegexFilterPattern, TimeSpan moveTime)
        {
            string pattern = tracknameRegexFilterPattern;
            TimeSpan offset = moveTime;

            foreach (AudioTrack t in trackList)
            {
                if (!t.Locked && Regex.IsMatch(t.Name, pattern))
                {
                    t.Offset += offset;
                }
            }
        }

        public static void EvaluateOffsets(TrackList<AudioTrack> trackList)
        {
            Dictionary<AudioTrack, TimeSpan> mapping = new Dictionary<AudioTrack, TimeSpan>();
            string timeFormat = "G";
            IFormatProvider numberFormat = new CultureInfo("en-US");

            // get synced offsets (right project file must be loaded)
            foreach (AudioTrack t in trackList)
            {
                if (t.Volume == 0f) continue; // skip silenced tracks
                mapping.Add(t, t.Offset);
            }

            // unlock all tracks
            trackList.ToList().ForEach(x => x.Locked = false);

            // sync all tracks by timestamp
            TimestampAlign(trackList, null);

            // reference time for offset calculation: reference time is taken from the first timebase track
            TimeSpan reference = trackList[0].Offset;
            if (trackList[0].Name.StartsWith("NAF_230312"))
            {
                reference = trackList[1].Offset;
            }

            foreach (AudioTrack t in mapping.Keys)
            {
                TimeSpan offset = t.Offset - mapping[t] - reference;
                Console.WriteLine(t.Name + ";" + offset.Ticks + ";" + offset.ToString(timeFormat, numberFormat));
            }
        }
    }
}
