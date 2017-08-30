# Hotsapi.Uploader

Uploads Heroes of the Storm replays to hotsapi.net

![Screenshot](http://hotsapi.net/img/uploader.png)

# Installation

* Requires .NET Framework 4.6.2 or higher
* [__Download__](https://github.com/Poma/Hotsapi.Uploader/releases) **"Setup.exe"** from [Releases](https://github.com/Poma/Hotsapi.Uploader/releases) page (you don't need to download other files listed there) and run it

# Contributing

Coding conventions are as usual for C# except braces, those are in egyptian style ([OTBS](https://en.wikipedia.org/wiki/Indent_style#1TBS)). For repos included as submodules their coding style is used.

All logic is contained in `Hotsapi.Uploader.Common` to make UI project as thin as possible. `Hotsapi.Uploader.Windows` is responsible for only OS-specific tasks such as auto update, tray icon, autorun, file locations.

For the current to do list look in the [Issues](https://github.com/poma/Hotsapi.Uploader/commits/master) page