AudioAlign: Audio Synchronization And Analysis Tool
===================================================

AudioAlign is a tool written for research purposes to automatically synchronize audio and video recordings that have either been recorded in parallel at the same event or contain the same aural information.

AudioAlign is basically a GUI for the [Aurio](https://github.com/protyposis/Aurio) library with a little bit of glue code in between.

To see what AudioAlign can do, check the demo videos linked below.


Use cases
---------

AudioAlign has been developed for a research project with the goal to automatically synchronize audio and video recordings, recorded at the same time at the same event, e.g. a speech or a music concert. The idea was to synchronize all those videos taken from the crowd and combine them to

* generate multicamera cuts by switching between perspectives ([video](https://www.youtube.com/watch?v=QXQVPXjR3Lc)),
* create videos with full or at least better event coverage,
* replace bad quality audio or video tracks with better ones, or
* detect interesting moments (where many recordings have been captured at the same time).

AudioAlign can be used for a lot more uses cases though, including

* video mashups ([video](https://www.youtube.com/watch?v=cdv4-gOxxZ0))
* comparison of live performances ([video](https://www.youtube.com/watch?v=4yUSLa4K3GE))
* synchronization of different cover interpretations ([video](https://www.youtube.com/watch?v=Jo2XPXUmkK0))
* voice dubbing ([video](https://www.youtube.com/watch?v=f89isFfLgvg))
* ground truth creation ([website](http://protyposis.github.io/JikuMVD-SynchronizationGroundTruth/))
* evaluation of fingerprinting algorithms
* ...


Building & Running
------------------

AudioAlign requires Visual Studio 2013 and the .NET Framework 4.0. It depends on Aurio, which is included as a Git submodule, and OxyPlot, which is automatically downloaded by Visual Studio through NuGet when compiling for the first time. Make sure that NuGet downloads are enabled by checking `Allow NuGet to download missing packages` in `Tools -> Options -> NuGet Package Manager`.

1. Clone the repository and Aurio submodule: `git clone --recursive https://github.com/protyposis/AudioAlign.git`
2. Setup FFmpeg dependencies, see `Aurio\libs\ffmpeg\ffmpeg-prepare.txt`
3. Open `Aurio.sln` in Visual Studio and hit the Start button


Documentation
-------------

Not available yet. If you have any questions, feel free to open an issue!


License
-------

Copyright (C) 2010-2015 Mario Guggenberger <mg@protyposis.net>.
This project is released under the terms of the GNU Affero General Public License. See `LICENSE` for details.
