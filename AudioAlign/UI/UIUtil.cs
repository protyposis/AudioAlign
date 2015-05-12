using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace AudioAlign.UI {
    class UIUtil {
        /// <summary>
        /// Copied from: http://msdn.microsoft.com/en-us/library/system.windows.frameworktemplate.findname.aspx
        /// </summary>
        public static childItem FindVisualChild<childItem>(DependencyObject obj) where childItem : DependencyObject {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++) {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is childItem)
                    return (childItem)child;
                else {
                    childItem childOfChild = FindVisualChild<childItem>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }

        /// <summary>
        /// copied from: http://stackoverflow.com/questions/4139341/wpf-listbox-onscroll-event
        /// </summary>
        public static List<T> GetVisualChildCollection<T>(object parent) where T : Visual {
            List<T> visualCollection = new List<T>();
            GetVisualChildCollection(parent as DependencyObject, visualCollection);
            return visualCollection;
        }

        /// <summary>
        /// copied from: http://stackoverflow.com/questions/4139341/wpf-listbox-onscroll-event
        /// </summary>
        private static void GetVisualChildCollection<T>(DependencyObject parent, List<T> visualCollection) where T : Visual {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++) {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T) {
                    visualCollection.Add(child as T);
                }
                else if (child != null) {
                    GetVisualChildCollection(child, visualCollection);
                }
            }
        }
    }
}
