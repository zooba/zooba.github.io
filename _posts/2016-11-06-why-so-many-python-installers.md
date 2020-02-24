---
layout: post
title: Why are there so many Python installers?
slug: why-so-many-python-installers
categories: Python
tags:
- python
- windows
---

Those who have been following Python development on Windows recently will be aware that I've been actively redeveloping the installer. And if you've been watching closely you'll know that there are now many more ways to install the official [python.org](https://python.org) release than in the past, not even including distributions such as [WinPython](https://winpython.github.io/) or [Anaconda](https://continuum.io/).

In this post, I'm going to discuss each of the ways you can install the official releases of Python (since version 3.5), provide some context on when and why you would choose one over another, and discuss the positives and negatives of each approach.

# History

Historically, there was one single [MSI installer](https://en.wikipedia.org/wiki/Windows_Installer) available that was intended to cover the needs of all Python users.

![Python 3.4.4 installer](/assets/python344installer.png)

This installer would allow you to select a target directory and some features from its user interface or the command line (if you know the magic words), and would generally install the full distribution with all entry points (shortcuts, etc.).

Unfortunately, due to the nature of how MSIs work, there are some limitations that affect the user experience. The most major of these is the fact that MSIs cannot decide whether [elevate](https://en.wikipedia.org/wiki/User_Account_Control) as part of the install - it has to be hardcoded. As a result, the old installer always requires administrative privileges _just in case_ you choose to install for all users. This prevents installation of Python on machines where you do not have full control over the system.

Secondly, while Python is often seen as one monolithic package, it is actually made up of a number of unrelated components. For example, the test suite is often not required for correct operation, nor is the documentation and often the development headers and libraries. While MSIs do support optional features, they tend to encounter issues when performing upgrades between versions (such as forgetting which options you had selected), and in general you always need to carry around the optional components even if you're never going to install them.

Finally, some operations that are not simple file installations can be complicated. For example, when [pip](https://pip.pypa.io/) is installed or the standard library is precompiled, the MSI executes a background task rather than normally installing files. Without careful configuration of the MSI, these files will not be properly uninstalled or repaired, and issues in the extraction process can cause the entire task to fail. At worst, the uninstall step could fail, which can make it impossible to uninstall Python.

# Modern Era

The issues described above have been addressed by the installers available since Python 3.5. However, there are also other uses for Python that do not lend themselves to a regular installer. For example, applications that want to include Python as a runtime dependency may not want to install a global copy of Python, build machines may require semi-public but non-conflicting installs of different versions, and platform-as-a-service web hosts may not allow normal installers.

Since Python 3.5.2, the official Python releases have been made available as executable installers, embeddable ZIP packages, nuget packages and Azure site extensions. There are also a range of third-party distributions that include the official Python binaries, along with other useful tools or libraries.

> **How do I know if a third-party distribution has the official binaries?** Find the install directory, right-click each of the exe, dll or pyd files and select Properties, then Digital Signatures. If the signature is from the Python Software Foundation, it's the official binary and has not been modified. If there is a different signature or no signature, it may not be the same as what is released on [python.org](https://python.org).

> ![The Python Software Foundation certificate](/assets/psfcertificate.png)

## Executable installers

The executable installers are the main way that users download Python, and are the featured downloads at [python.org](https://python.org). I think of these as the Python Developer Kit.

![Python 3.5.0 installer](/assets/python350installer.png)

These installers provide the most flexible user interface, include all dependencies such as system updates and the [Python launcher](https://docs.python.org/3/using/windows.html#python-launcher-for-windows), generate shortcuts for the interpreter, the manuals and the IDLE editor, and correctly support upgrades without forgetting about feature selection.

Two versions of the executable installer are available for any given release - one labelled "executable installer" and the other "web-based installer".

The web-based installer is typically a small initial download (around 1MB), which gets you the installer UI shown above. After you have selected or deselected optional components, the minimum set of packages necessary to install Python will be downloaded and installed. This makes it easy to minimize overall download size since unused or unnecessary components are never downloaded, though it does require that you be connected to the internet at install time. (There's also a [command-line option](https://docs.python.org/3/using/windows.html#installing-without-downloading) to download all the packages you may ever need, which will then be used later instead of downloading them over and over again.)

The other installer includes the default set of features in the EXE itself. As a result, the initial download is around 30MB, but in most cases you can install without requiring any further internet access. For a single installation, your download will likely be 3-5MB larger compared to using the web-based installer, but if you use it to install on multiple machines then you'll likely come out ahead.

Both executable installers result in identical installations and can be automated with identical [command-line options](https://docs.python.org/3/using/windows.html#installing-without-ui). As I mentioned above, I think of this as the Python Developer Kit, which is why there are optional features to download debugging symbols or a complete debug build, which are not available in any other options. The Python Developer Kit provides everything necessary for someone to develop a complete Python application.

> **What about having a single MSI installer?** There's a section coming up about this. Just keep reading.

## Embeddable package

If the executable installer is the Python Developer Kit, then the embeddable package is the Python runtime redistributable. Rather than trying to be an easy-to-use installer, this package is a simple ZIP file containing the bare minimum of Python required to run applications. This includes the `python[w].exe` executables, the `python35.dll` (or later) and `python3.dll` modules, the standard library extension modules (`*.pyd`), and a precompiled copy of the standard library stored in another ZIP file.

The resulting package is about 7MB to download and around 12MB when extracted. Documentation, tools, and shortcuts are not included, and the embeddable package does not reliably build and install packages. However, once your application is ready, rather than instructing users to install Python themselves, you can include the contents of this package in your own installer. (For example, Microsoft's [command-line tools for Azure](https://github.com/Azure/azure-cli) will likely do this, and installers created using [pynsist](http://pynsist.readthedocs.io/) can include this package automatically.)

Using the embeddable package allows you to distribute applications on Windows that use Python as a runtime without exposing it to your users. By default, a configuration file is also included to force the use of isolated mode and prevents environment variables and registry settings from affecting it (`python36._pth` on Python 3.6; `pyvenv.cfg` for Python 3.5). On Python 3.6 this file can also specify additional search paths. If your application is hosting Python, you can also choose not to distribute `python.exe` or any extension modules that are not used in your application.

There is no support for pip, [setuptools](https://setuptools.readthedocs.io/) or [distutils](https://docs.python.org/3/library/distutils.html) in the embeddable package, since the idea is that you will develop against the Python Developer Kit and then lock your dependencies when you release your application. Depending on the installer technology you are using for your application, you will probably <a><acronym title="Create a copy of a package within your own package, and update all imports to use your private copy.">vendor</acronym></a> any third-party packages by copying them directly into the directory with your Python code.

See [this blog post](https://blogs.msdn.microsoft.com/pythonengineering/2016/04/26/cpython-embeddable-zip-file/) for more information about how to take advantage of the embeddable distribution.

## Nuget package

[Nuget](https://nuget.org) is a packaging technology typically used on Windows to manage development dependencies. There are many packages available as source code or pre-built binaries, mostly for .NET assemblies, as well as build tools and extensions.

There are four Python packages available on nuget, released under my name (`steve.dower`) but built as part of the official [python.org](https://python.org) releases. The packages are:

* [python](https://www.nuget.org/packages/python/) - the latest 3.x 64-bit
* [pythonx86](https://www.nuget.org/packages/pythonx86/) - the latest 3.x 32-bit
* [python2](https://www.nuget.org/packages/python2/) - the latest 2.x 64-bit
* [python2x86](https://www.nuget.org/packages/python2x86/) - the latest 2.x 32-bit

These may be referenced by projects in [Visual Studio](https://visualstudio.com/) or directly using [nuget.exe](https://dist.nuget.org/index.html) to easily install a copy of Python into a build directory. It will typically install into a directory like `packages\python.3.5.2\tools\python.exe`, though this can often be customised.

```
rem Install Python 2.7
nuget.exe install -OutputDirectory packages python2

rem Add -Prerelease to get Python 3.6
nuget.exe install -OutputDirectory packages -Prerelease python

rem More options are available
nuget.exe install -Help
```

The contents of the nuget package is somewhere between the full installation and the embeddable package. The headers, libs and pip are included so that you can install dependencies or build your own modules. The standard library is not zipped, but also does not include the CPython test suite or libraries intended for user interaction. Operating system updates are not included, so you will need to ensure your build machine is up to date before using these packages.

There is no configuration in these packages to restrict search paths or environment variables, as these are very important to control in build definitions. As a result, there is a high likelihood that a regular installation of Python may conflict with these packages. In general, it's best to avoid installing Python on build machines where you are using these packages. If you need a full installation, avoid using the nuget packages or test for conflicts thoroughly. (Note that conflicts typically only occur within the same `x.y` version, so you can safely install 2.7 and use the 3.5 nuget packages.)

## Azure Site Extensions
> **Update 2019:** These packages have been deprecated and removed. This section is of historical interest only.

> **Note:** This particular package is released by Microsoft and is managed by my team there. The Python Software Foundation is not responsible for this package.

[Azure App Service](https://tryappservice.azure.com/) is a platform-as-a-service offering for web services (including web apps, mobile backends, and triggered jobs). It uses [site extensions](https://www.siteextensions.net/) to customise and enhance your web services, including a range of Python versions to simplify configuration of Python-based servers.

Because web services are sensitive to even the smallest change in a dependency, each version is available as its own package. This allows you to be confident that when your site uses one of these it is not going to change without you explicitly updating your site. The current packages available at time of writing are:

* python352x64
* python352x86
* python351x64
* python2712x64
* python2712x86
* python2711x64

The contents of these packages is almost entirely unmodified from the official [python.org](https://python.org) releases. Some extra files for correct installation, configuration and behaviour of the web server are included, as well as copies of pip, setuptools, and [certifi](https://pypi.org/project/certifi/). Occasionally a package will include targeted patches to fix or work around issues with the platform, but we always aim to upstream fixes as soon as possible. Under the hood, these are simply nuget packages that can also be installed using [nuget.exe](https://dist.nuget.org/index.html) on any copy of Windows.

```
C:\> nuget.exe list python -Source https://www.siteextensions.net/api/v2/
python2711x64 2.7.11.3
python2712x64 2.7.12.2
python2712x86 2.7.12.1
python351x64 3.5.1.6
python352x64 3.5.2.2
python352x86 3.5.2.1
```

Visit [aka.ms/PythonOnAppService](https://aka.ms/PythonOnAppService) for the most up-to-date information about how to use these packages on Azure App Service.

# Hypothetical Futures

While that covers the current set of available installers, there are some further use-cases that are not as well served. In this section I will briefly discuss the cases that I am currently aware of and their status. There are no promises that official installation packages for these will ever be produced (bearing in mind that Python is developed almost entirely by volunteers with limited free time), but there is also nothing preventing third-parties from producing and distributing these formats.

> **Are you already distributing Python in any of these formats?** Let me know and I'm happy to link to you, provided I'm not concerned about the contents of your distribution.

## Nuget package for source/runtime dependency

Earlier I discussed the nuget packages as build tools, but the more common use of nuget packages is for build _dependencies_. Normally a project (typically a Visual Studio project, but nuget can also be used independently) will specify a dependency on a source or binary package and obtain build steps or configuration from a known location within the package.

Providing a nuget package containing either the Python source code or the embeddable package may simplify projects that host the runtime. These would predominantly be C/C++ projects rather than pure Python projects, but some installer toolkits may prefer a ready-to-embed nuget package rather than a plain ZIP file.

There has not been much demand for this particular format. In general, a C/C++ project can make equally good use of the current nuget packages, and would require those for the headers and libraries anyway, while the embeddable package is not always suitable for installation completely unmodified. These reduce the value of a dependency nuget package to nearly zero, which is why we currently don't have one.

## Universal Windows Platform

The [Universal Windows Platform](https://msdn.microsoft.com/en-us/windows/uwp/get-started/universal-application-platform-guide) is part of Windows 10 and specifies a common API set that is available across all Windows devices. This includes PCs, tablets, phone, [IoT Core](https://developer.microsoft.com/en-us/windows/iot), [XBox](http://www.xbox.com/xbox-one/), [HoloLens](https://www.microsoft.com/microsoft-hololens/), and likely any new Windows hardware into the future.

Providing a UWP package of Python would allow developers to distribute Python code across all of these platforms. Indeed, the team behind IoT Core have already provided their version of [this package](https://github.com/ms-iot/python/releases). However, as the API set is not always compatible with the Win32 API, this task requires supporting a new platform within Python (that is, `sys.platform` would return a value other than `'win32'`). Currently nobody has completely adapted Python for UWP, added the extensions required to access new platform APIs, or fully implemented the deployment tools needed for this to be generally useful (though the IoT Core support is a huge step towards this).

## Administrative Deployment

System administrators will often deploy software to some or all machines on their network using management tools such as [Group Policy](https://support.microsoft.com/en-us/kb/816102) or [System Center](https://docs.microsoft.com/en-us/sccm/apps/deploy-use/deploy-applications). While it is possible to remotely install from the executable installers, these tools often require or have enhanced functionality when the installer is a pure MSI.

Unfortunately, the issues and limitations of MSI described at the start of this post still apply. It is not possible for an MSI to install all required dependencies, create an MSI that can run without administrator privileges, and robustly ensure that upgrade and remove operations behave correctly. However, it would be possible to produce a suitable MSI and installation instructions for the limited use case of administrative deployment. Such a package would likely have these characteristics:

* Fails if certain operating system updates are missing
* Always requires administrator privileges
* Only allows installation for all users
* Only allows configuration at the command line (via `msiexec`)
* Requires a separate task to precompile the standard library and install pip
* Requires additional cleanup task when uninstalling
* Prevents the executable installer from installing for all users

System administrators would be responsible for following the documentation associated with such an MSI, and I have no doubt that most are entirely capable of doing this. However, as this would not be a good experience for most users it cannot be the default or recommended installer. I'm aware that there are some people who are grieved by this, but interactive installs are vastly more common and so take priority when determining what to offer from [python.org](https://python.org).

# Summary

Installing Python on Windows has always been a fairly reliable process. The ability to select precisely which version you would like without fear of damaging system components allows a lot of confidence that is not always available on other platforms. Improvements in recent releases make it easier to install, upgrade and manage Python, even for non-administrator users.

We have a number of different formats in which Python may be obtained depending on your intended use. The [executable installers](https://www.python.org/downloads/windows) provide the full Python Developer Kit; the [embeddable package](https://docs.python.org/3/using/windows.html#embedded-distribution) contains the runtime dependencies; [nuget packages](https://www.nuget.org/) allow easy use of Python as a build tool; and site extensions for [Azure App Service](https://aka.ms/pythononappservice) make it easier to manage Python as a web server dependency.

There is also potential to add new formats in the future, either through third-party distributions or as new maintainers volunteer their time towards core development. For an open-source project that is run almost entirely on volunteer time, Python is an amazing example of a robust, trustworthy product with as much flexibility as any professionally developed product.

> Discussion of this post is welcome here in the comments. If you are having issues installing Python, please file an issue on [bugs.python.org](https://bugs.python.org).
