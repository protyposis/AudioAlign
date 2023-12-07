# AudioAlign: Audio Synchronization And Analysis Tool

AudioAlign is a research tool to investigate automatic synchronization of audio and video recordings that have either been recorded in parallel at the same event or contain the same aural information. It is designed as a GUI for the [Aurio](https://github.com/protyposis/Aurio) library.

To see what AudioAlign can do, check the demo videos linked below.

![Screenshot of the GUI](audioalign1.png)


## Use cases

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


## What's new

See [CHANGELOG](CHANGELOG.md).


## Development Requirements

* Visual Studio 2022
* .NET SDK 6.0


## Documentation

### Controls & Shortcuts

* Audio and video files can be drag & dropped into the timeline
  * Multiple files will be added as multiple tracks
  * Hold `SHIFT` to add the files as a single concatenated track
* Navigating the multitrack view
  * _Click anywhere_ or _drag the caret_ in the time scale to set the current (playback) position
  * Press `SPACE` to start/pause playback
  * Vertically _drag_ the bottom of a track to resize its height
  * _Scroll the mouse wheel_ to scale the time resolution (zoom into/out of the timeline) at the current position 
    * Hold `CTRL` to smoothly scroll the timeline
    * Hold `CTRL + SHIFT` to page through the timeline 
  * Select a track by _clicking_ its title bar
    * Hold `CTRL` to select multiple tracks
    * Hold `SHIFT` to select a range of tracks
  * Move tracks on the timeline by _dragging_ them horizontally

## Publications

> Mario Guggenberger, Mathias Lux, and Laszlo Böszörmenyi. 2012. [AudioAlign – Synchronization of A/V-Streams Based on Audio Data](http://protyposis.net/publications/). 2012 IEEE International Symposium on Multimedia. Irvine, CA, USA, 2012, pp. 382-383. DOI=http://dx.doi.org/10.1109/ISM.2012.79

> Mario Guggenberger. 2015. [Aurio: Audio Processing, Analysis and Retrieval](http://protyposis.net/publications/). In Proceedings of the 23rd ACM international conference on Multimedia (MM '15). ACM, New York, NY, USA, 705-708. DOI=http://dx.doi.org/10.1145/2733373.2807408

## Support

For questions and issues, please open an issue on the issue tracker. Commercial support, development
and consultation is available through [Protyposis Multimedia Solutions](https://protyposis.com).

## License

Copyright (C) 2010-2023 Mario Guggenberger <mg@protyposis.net>.
This project is released under the terms of the GNU Affero General Public License. See `LICENSE` for details.
