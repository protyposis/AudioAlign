using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace AudioAlign.UI {
    [TemplatePart(Name="PART_ResizeThumb", Type=typeof(Thumb))]
    public class ResizeDecorator : ContentControl {

        static ResizeDecorator() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ResizeDecorator), new FrameworkPropertyMetadata(typeof(ResizeDecorator)));
        }

        public ResizeDecorator() {
            this.Loaded += new RoutedEventHandler(ResizeDecorator_Loaded);
        }

        private void ResizeDecorator_Loaded(object sender, RoutedEventArgs e) {
            Thumb resizeThumb = GetTemplateChild("PART_ResizeThumb") as Thumb;
            if (resizeThumb != null) {
                resizeThumb.DragDelta += new DragDeltaEventHandler(resizeThumb_DragDelta);
                resizeThumb.MouseDoubleClick += new System.Windows.Input.MouseButtonEventHandler(resizeThumb_MouseDoubleClick);
            }
        }

        private void resizeThumb_DragDelta(object sender, DragDeltaEventArgs e) {
            double newHeight = Math.Max(MinHeight, ActualHeight + e.VerticalChange);
            SetValue(HeightProperty, newHeight);
            e.Handled = true;
        }

        void resizeThumb_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            ClearValue(HeightProperty);
            e.Handled = true;
        }

    }
}
