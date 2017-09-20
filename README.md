# Hotsapi.Uploader [![Build status](https://ci.appveyor.com/api/projects/status/0tg5u1yev3l8p2qv/branch/master?svg=true)](https://ci.appveyor.com/project/poma/hotsapi-uploader/branch/master) [![Join the chat at https://gitter.im/hotsapi/general](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/hotsapi/general)

Uploads Heroes of the Storm replays to [hotsapi.net](http://hotsapi.net) ([repo link](https://github.com/poma/hotsapi))

![Screenshot](http://hotsapi.net/img/uploader.png)

# Installation

* Requires .NET Framework 4.6.2 or higher
* [__Download__](https://github.com/Poma/Hotsapi.Uploader/releases) **"HotsApiUploaderSetup.exe"** from [Releases](https://github.com/Poma/Hotsapi.Uploader/releases) page (you don't need to download other files listed there) and run it

# Contributing

Coding conventions are as usual for C# except braces, those are in egyptian style ([OTBS](https://en.wikipedia.org/wiki/Indent_style#1TBS)). For repos included as submodules their coding style is used.

All logic is contained in `Hotsapi.Uploader.Common` to make UI project as thin as possible. `Hotsapi.Uploader.Windows` is responsible for only OS-specific tasks such as auto update, tray icon, autorun, file locations.

For the current to do list look in the [Project](https://github.com/poma/Hotsapi.Uploader/projects/1) page
