# Changelog

All notable changes to this project will be documented in this file. See [commit-and-tag-version](https://github.com/absolute-version/commit-and-tag-version) for commit guidelines.

## [1.5.1](https://github.com/protyposis/AudioAlign/compare/v1.5.0...v1.5.1) (2023-12-09)


### Bug Fixes

* silent startup crash on missing native dependency ([fa11c05](https://github.com/protyposis/AudioAlign/commit/fa11c053be5bbb4f030ca4a8882280878b9a8c0b))

## 1.5.0 (2023-12-07)

### Features

* allow skipping of missing project assets on load
* improve scheduling of many CC operations
* display number of matches
* bump Aurio to 4.0.0 (incl. FFmpeg 6.0)

## 2018-03-22 741d27c

* Update Aurio to [v5af5339](https://github.com/protyposis/Aurio/blob/main/CHANGELOG.md#2018-03-22-5af5339)
* Requires Visual Studio 2017 (due to Aurio update to .NET Standard 2.0 / .NET Core 2.0)

## 2017-10-15 d87bb4e

* Update Aurio to [v14c7c92](https://github.com/protyposis/Aurio/blob/main/CHANGELOG.md#2017-10-15-14c7c92)
* Validate matches before executing aligment and show error message box if validation fails
* Show error message box if added file cannot be read
* Report progress during HK/CP match finding and filtering

## 2017-02-06 ec054a6
* Update Aurio to [v3e703cd](https://github.com/protyposis/Aurio/blob/main/CHANGELOG.md#2017-02-06-3e703cd)
* Concatenated tracks consisting of multiple files (hold `SHIFT` key when adding)

## 2016-03-01 8539db8

* Update Aurio to [vfe49ea5](https://github.com/protyposis/Aurio/blob/main/CHANGELOG.md#2016-03-01-fe49ea5)
* Support for compressed audio and video file formats
* Close file handles after use
