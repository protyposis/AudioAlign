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
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace AudioAlign.UI
{
    /// <summary>
    /// http://msdn.microsoft.com/en-us/library/ms748975.aspx
    /// </summary>
    class NonClientRegionAPI
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int cxLeftWidth; // width of left border that retains its size
            public int cxRightWidth; // width of right border that retains its size
            public int cyTopHeight; // height of top border that retains its size
            public int cyBottomHeight; // height of bottom border that retains its size
        };

        [DllImport("DwmApi.dll")]
        public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

        public static void Glassify(Window window)
        {
            // GLASS EFFECT BACKGROUND
            // http://msdn.microsoft.com/en-us/library/ms748975.aspx
            try
            {
                // Obtain the window handle for WPF application
                IntPtr mainWindowPtr = new WindowInteropHelper(window).Handle;
                HwndSource mainWindowSrc = HwndSource.FromHwnd(mainWindowPtr);
                mainWindowSrc.CompositionTarget.BackgroundColor = Color.FromArgb(0, 0, 0, 0);

                // Set Margins
                NonClientRegionAPI.MARGINS margins = new NonClientRegionAPI.MARGINS();
                margins.cxLeftWidth = -1;
                margins.cxRightWidth = -1;
                margins.cyTopHeight = -1;
                margins.cyBottomHeight = -1;

                int hr = NonClientRegionAPI.DwmExtendFrameIntoClientArea(
                    mainWindowSrc.Handle,
                    ref margins
                );
                window.Background = Brushes.Transparent;
                //
                if (hr < 0)
                {
                    //DwmExtendFrameIntoClientArea Failed
                }
            }
            // If not Vista, paint background white.
            catch (DllNotFoundException)
            {
                //Application.Current.MainWindow.Background = Brushes.White;
            }
        }
    }
}
