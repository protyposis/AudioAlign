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
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace AudioAlign
{
    /// <summary>
    /// Interaction logic for About.xaml
    /// </summary>
    public partial class About : Window
    {
        public About()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            InitializeComponent();

            var assembly = Assembly.GetEntryAssembly();
            var assemblyInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            appName.Text = assemblyInfo.ProductName;
            appVersion.Text =
                assemblyInfo.ProductVersion
                + " ("
                + assemblyInfo.FileVersion
                + " / "
                + assembly.GetName().Version
                + ")";
            appCopyright.Text = assemblyInfo.LegalCopyright;

            licenseTextBox.Text =
                ReadEmbeddedResourceFileText("AudioAlign.NOTICE")
                + Environment.NewLine
                + Aurio.License.Info;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private static string ReadEmbeddedResourceFileText(string filename)
        {
            string text = String.Empty;
            using (
                Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(filename)
            )
            {
                using StreamReader reader = new(stream);
                text = reader.ReadToEnd();
            }
            return text;
        }
    }
}
