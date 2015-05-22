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
