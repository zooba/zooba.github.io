---
layout: post
title: What's Coming for the Python 3.5 Installer?
slug: the-python-3-5-installer
category: Python
tags:
- install
- installer
- MSI
- python
- Windows
---

Last year at PyCon US, I volunteered to take over maintenance and development of the Python installers for Windows. My stated plan was to keep building the installer for Python 2.7 without modification, and to develop a new installer for Python 3.5. In this post, I'm going to show some of the changes I've been working on. Nothing is settled yet, and more changes are basically guaranteed before the first releases occur, but I'm happy that we'll soon have a more powerful and flexible installer.

The installer will first be available for Python 3.5.0 alpha 1, due to be released in February.

# Changes You Will Notice

The most dramatic change (and the most likely to be removed before the final release) is new default installation locations.

[![The first page of the Python 3.5 installer, showing "Install for All Users", "Install Just for Me", and "Customize installation" buttons.](/assets/InstallerPage1-300x181.png)](/assets/InstallerPage1.png)

Installing a copy of Python for all users on a machine and allowing everyone to modify it (the default under Python 3.4 and earlier) is a [massive security hole](http://bugs.python.org/issue1284316). When installed into Program Files, only administrators can modify the shared files, and so users are better protected from malicious or accidental modifications.

Those who have used the Just for Me option in previous versions of Python are likely to have been surprised when it did not work as expected. For Python 3.5, this is now a true per-user installation. All files are installed into a directory than can only be accessed by the current user and the installation will work without administrative privileges.

The first two buttons on this page are single-click installs, meaning you'll get all the default features and options, including pip and IDLE. For most users, these will dramatically simplify the process of installing Python.

However, many of us (myself included) like to be a bit more selective when we install Python. The third button, Customize installation, is for us.

[![The Optional Features page of the Python 3.5 installer, showing "pip", "tcl/tk and IDLE", and "Python test suite" checkboxes.](/assets/InstallerPage2-300x181.png)](/assets/InstallerPage2.png)

There are two pages of options. The first is a list of features that can be added or removed independently of the rest of the installation. Compared to the old-style tree view, the simple list of checkboxes makes it easier to see what each feature provides. This is also the screen you'll see when you choose to modify an existing installation.

[![The Advanced Options page of the Python 3.5 installer.](/assets/InstallerPage3-300x181.png)](/assets/InstallerPage3.png)

The second page is advanced options, including the install location which (currently) defaults to the legacy directory, allowing you to install Python 3.5 identically to the older versions with the same amount of clicking. Right now, the options are basically identical to previous versions, but they are no longer mixed up with installable features. The way they are implemented has also been improved to be more reliable.

[![The success page of the Python 3.5 installer, showing a thankyou to Mark Hammond and links to online documentation.](/assets/InstallerPage4-300x181.png)](/assets/InstallerPage4.png)

From here, the rest of the installation proceeds as you'd expect. The final page retains the familiar message (thanks, Mark!) and also adds some links into the online documentation.

# Changes You Will Not Notice

One interesting option you may have spotted on the Advanced Options page is a checkbox to install debugging symbols (.pdb files). These are handy if you work on or debug C extensions (for example, [Visual Studio's](https://visualstudio.com/vs/python) [mixed-mode C/Python debugging](https://docs.microsoft.com/visualstudio/python/debugging-mixed-mode-c-cpp-python-in-visual-studio) feature requires Python's PDB files), and this is an easy way to install them. Previously the symbol files were available in a separate ZIP, but now they are just a checkbox away.

But wait, doesn't this make the installer a larger download? Yes, or at least it would if the installer included the debugging symbols.

The biggest change to the installer is its architecture. Previously, it was a single MSI with a single embedded CAB that contained all the files. The new installer is a collection of MSIs (currently 12, though this could change), CABs (currently 16) and a single EXE. The EXE is the setup program shown above, while the CABs contain the install files and the MSIs have the install logic.

With this change, it is possible to lazily download MSIs and CABs as needed. Although it's not marked in the screenshot above, the "Install debugging symbols" option will require an active internet connection and will download symbols on demand. In fact, it's trivially easy to download all the components on demand, which reduces the initial download to less than 1MB.

My initial plan is to release four downloadable installers for Python 3.5.0 alpha1: two "web" installers (32-bit and 64-bit) and two "offline" installers that include the default components (download size is around 20MB, and it includes everything that was included in earlier versions). Depending on feedback and usage, this may change by the final release, but initially I want to offer useful alternatively without being too disruptive.

Another change that is part of the build process is code signing. Previously, only the installer was signed, which meant that undetectable changes could be made to python.exe or pythonXY.dll after installation. As part of reworking the installer, I've also moved to signing every binary that is part of a Python installation. This improves the level of trust for those who choose to validate signatures, as well as using the [signed UAC dialog](https://en.wikipedia.org/wiki/User_Account_Control) rather than the unsigned one when running Python as an administrator.

# Changes For Administrators

For those who have scripted or automated Python installation from the old MSIs, things are going to change a bit. I believe these are for the better, as we never previously really documented and supported command-line installation, and I'll be interested in the feedback from early adopters.

The first concern likely to arise is the web installers - how do I avoid downloading from the Python servers every time I install? What if I have to install on two hundred machines? Two thousand? The easiest way is to simply download everything once with the "/layout" option:

```
python-3.5.0a1.exe /layout \\shared\python\3.5.0a1
```

This will not install Python, but it will create a folder on a shared machine (or a local path) and download all the components into that folder. You can then run `python-3.5.0a1.exe` from that location and it will not need to download anything. Currently the entire layout is around 26MB for each of the 32-bit and 64-bit versions.

[![The files that make up the Python 3.5 installer layout](/assets/InstallerLayout-300x110.png)](/assets/InstallerLayout.png)

To silently install, you can run the executable with `/quiet` or `/passive`, and customisation options can be provided as properties on the command-line:

```
python-3.5.0a1.exe /quiet TargetDir=C:\Python35 InstallAllUsers=1 Include_pip=0 AssociateFiles=0 CompileAll=1
```

I'm not going to document the full list yet, as they may change up until the final release, but there will be a documentation page dedicated to installing and configuring Python on Windows.

# How Can I Try This Out Early?

I'm still very actively working on this, but you can get my changes from [hg.python.org/sandbox/steve.dower](https://hg.python.org/sandbox/steve.dower) on the Installer branch. The build files are in Tools/msi and will (should) work with either Visual Studio 2013 or Visual Studio 2015 Preview.

# Where Do I Complain About This?

I am keen to hear constructive feedback and concerns, so come and find the threads at [python-dev](https://mail.python.org/mailman/listinfo/python-dev). Nothing is unchangeable, and the Python community gets to have its say, though right now I'm looking to stabilise things up until alpha so please don't be too upset if your suggestion doesn't appear in the first release.

If you're at all angry or upset, please make sure you've read the entire post before sharing that anger with everyone else. (That's just general good advice.)
