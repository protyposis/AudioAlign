using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;

namespace AudioAlign.UI {
    /// <summary>
    /// Source: http://stackoverflow.com/a/662232
    /// </summary>
    public class ListBoxItemIndexConverter : IValueConverter {

        public object Convert(object value, Type TargetType, object parameter, CultureInfo culture) {
            ListBoxItem item = (ListBoxItem)value;
            ListBox listView = ItemsControl.ItemsControlFromItemContainer(item) as ListBox;
            int index = listView.ItemContainerGenerator.IndexFromContainer(item) + 1;
            return index.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
