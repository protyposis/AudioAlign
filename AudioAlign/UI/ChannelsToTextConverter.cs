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
