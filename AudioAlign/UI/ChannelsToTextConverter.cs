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
using System.Linq;
using System.Text;
using System.Windows.Data;

namespace AudioAlign.UI {
    class ChannelsToTextConverter : IValueConverter {

        private static Dictionary<int, string> channelTexts = new Dictionary<int, string>() {
            {1, "mono"}, 
            {2, "stereo"}, 
            {3, "2.1"},
            {4, "quadro"},
            {6, "5.1"},
            {7, "6.1"},
            {8, "7.1"},
            {9, "7.2"}
        };

        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            if (value is int) {
                int channels = (int)value;
                if (channelTexts.ContainsKey(channels)) {
                    return channelTexts[channels];
                }
            }

            return "-";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            throw new NotImplementedException();
        }

        #endregion
    }
}
