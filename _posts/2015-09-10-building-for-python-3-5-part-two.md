---
layout: post
title: Building Extensions for Python 3.5 Part Two
slug: building-for-python-3-5-part-two
category: Python
tags:
- compiler
- crt
- msvc
- msvcrt
- python
- ucrt
- Windows
---

This was never meant to become a series, but unfortunately after posting what is now [part one](/blog/building-for-python-3-5/), we found a serious regression that led us to release [Python 3.5.0rc4](https://www.python.org/downloads/release/python-350rc4/).

If you haven't already, I recommend going back and reading part one up until the point where it says to come back here. That will fill you in on the background.

This is the point where I ought to make the "seriously, I'll wait" joke, knowing full well that nobody is going to read the old post. Instead, I'm going to write one sentence, and if you understand the words in it, you have enough background to continue reading.

> MSVC 14.0 (and later) and the UCRT give us independence from compiler versions, provided you build with /MT and force `ucrtbase.dll` to be dynamically linked.

If you scratched your head about any part of that, [go and read part one](/blog/building-for-python-3-5/). You'll thank me in about two seconds.

# The /MT Problem
Previously, I discussed some of the problems that arise when compiling with the `/MT` option. Mostly, because the option needs to be specified throughout all your code, it was going to cause issues with static libraries that had already been built.

Well, we found [one more problem](http://bugs.python.org/issue25027).

---

> I want to stop for a second to thank **Christoph Golhke** for all his help through the 3.5.0 RCs.

For those who don't know, Christoph maintains an [epic collection](http://www.lfd.uci.edu/~gohlke/pythonlibs/) of wheels for Windows. Every time you have trouble installing a package with pip, it's worth visiting his site to see if he has a wheel available for it. After downloading the wheel, you can pass its path to `pip install` and you'll get your package.

As we kept making changes to the build process, Christoph kept updating his own build steps and testing with literally hundreds of packages. His feedback has directly led to these changes to Python 3.5 that will make it much easier for everyone to have solid, future-proof builds of Python and extension modules.

---

Ready for the gory technical details of this new issue? Here we go.

Previously, we were statically linking functions from `vcruntime140.dll` into each extension module built for Python 3.5. This included, among other things, the DLL initialisation routines.

On the positive side, each extension module is now completely isolated from any others with respect to initialization, locale, debugging handlers, and other state.

On the negative side, it turns out initialization is a limited resource.

The CRT has a number of features that are thread-safe, such as [errno](https://msdn.microsoft.com/en-us/library/t3ayayh1.aspx). Each thread has its own `errno`, which means you do not need to perform locking around every operation that may set or use it.

To implement this per-thread state, the CRT uses fiber-local storage. On Windows, [fibers](https://msdn.microsoft.com/en-us/library/windows/desktop/ms682661.aspx) are a form of cooperative multithreading that work within threads (one process may contain many threads, one thread may contain many fibers), so the CRT uses fiber-local storage to ensure the finest-grain handling. If it used thread-local storage, different fibers would see the same values, and if it used a normal global, all threads would see the same value.

Fiber-local storage slots are allocated using [FlsAlloc()](https://msdn.microsoft.com/en-us/library/windows/desktop/ms682664.aspx). If you read that doc carefully, you'll see that a potential return value (error) is `FLS_OUT_OF_INDEXES`, which means you have exhausted the current process's supply of fiber-local storage.

The CRT doesn't like it if you've run out of fiber-local storage, because that means a lot of its stateful functionality will be broken. So it aborts.

How many slots do we get? It isn't documented (so it could change at any time), but here's how we can test it:

```
>>> import ctypes
>>> FlsAlloc = ctypes.windll.kernel32.FlsAlloc
>>> list([iter](https://docs.python.org/3.5/library/functions.html#iter)(lambda: FlsAlloc(None), -1))
[7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127]
>>> len(_)
121
```

On my machine, I can call `FlsAlloc` 121 times after starting the interpreter before it runs out of indices.

When each extension module has its own isolated initialization routine, each one will call `FlsAlloc`. Which means once I've loaded 121 extension modules, the 122nd will fail.

That sucks.

# Going back to /MD

The only reasonable fix for this is to switch back to building both Python and extensions using `/MD`. So we've done that.

That means that `vcruntime140.dll` is now a runtime dependency and must be included with Python 3.5. So we've done that.

That means that every extension module for Python 3.5 has to depend on `vcruntime140.dll` and therefore must always be build with MSVC 14.0.

Well, I don't think so.

Initialization is no longer isolated. You can load as many extension modules as you like and they'll all use the same fiber-local storage index because `vcruntime140.dll` is already initialized.

But what happens when someone comes along with MSVC 15.0 (note: not a real version number) and builds an extension that depends on `vcruntime150.dll`? When they publish a wheel of that extension and someone installs in on Python 3.5.0? That version of Python only includes `vcruntime140.dll`, so there will be an unresolved dependency and the extension will fail to load.

We've also made a change to fix this. The hint was in that last sentence: the _extension_ has an unresolved dependency.

# Extension Dependencies

It is no longer (and technically never was) Python's responsibility to provide dependencies required by extension modules. If you release an extension module, _you_ are responsible for including any dependencies, or instructing your users on how to get them.

But simultaneously, we really don't want to break `python setup.py bdist_wheel upload` (despite `upload` being [insecure](http://bugs.python.org/issue12226) and you should be using [twine](https://pypi.python.org/pypi/twine) or another uploader). If someone has MSVC 15.0 and builds a wheel using that version, somehow their users need to get `vcruntime150.dll` onto their machines.

So we fixed distutils.

When you build an extension using distutils (which is how most extensions are built, even if you think you're using another package), we know which compiler version is being used. This means we know which version of the CRT you are using and which version of `vcruntime###.dll` your extension depends on.

We also know which versions of `vcruntime###.dll` shipped with the target version of Python. Currently, this is a set that contains only `vcruntime140.dll` (it's also an internal implementation detail, so don't start depending on it), and for the lifetime of Python 3.5 it will only contain that value.

When you build an extension with MSVC, we find the redistributable `vcruntime###.dll` in your compiler install and _if it is not in the set of included files_, it is copied into your build.

Currently it's hard to see this in action, but once we have a newer MSVC to play with you'll be able to see it. Whenever you build an extension module, it will get the new `vcruntime` alongside it.

Because of how DLL loading works, if that version of the DLL has not been loaded yet then the one adjacent to the extension module will be used. If it _has_ been loaded, the one that is currently in memory will be used.

So really, we only need to include it once. The trick is making sure that it is loaded first. Ultimately, the only reliable way to do this is to include it everywhere.

Python 3.5 is always going to include `vcruntime140.dll` at this stage (though it may move from the top-level directory into `DLLs` at some point), so extensions built with MSVC 14.0 will always have it available.

Extensions built with a newer version of the compiler will include their own dependencies. _As they should._

Perhaps Python 3.6 will be built with a newer version of the compiler. We can still include `vcruntime140.dll`, so that extensions can be built with MSVC 14.0, as well as including the newer version. We simply add the new one to the list in distutils and builds stop including it.

In this way, we can ensure that, at least for simple extensions (the majority), the easiest path will remain compiler-version independent.

## Overriding the build process
One of the neat things about making the fix in the build process is that there are ways to override it. We can also make changes and improvements in bugfix releases if needed. It's also fairly easy for developers to patch their own system if they need to customize things further. However, a few simple customisations are available without having to touch any code.

There are three stages in the build process with respect to copying `vcruntime` (henceforth, "the DLL").

The first stage is locating the redistributable DLL. For regular installs of MSVC, these are in a known location, so once we've found `vcvarsall.bat` we find the DLL relative to that path.

The second stage is updating build options. If we didn't find the redistributable DLL, we will statically link it exactly as described [previously](/blog/building-for-python-3-5/). This is not recommended, but it is still allowed and supported (and there is an opt-in described below).

The third stage is copying the DLL to your build directory.

Here are a few tricks you can use to customize your build. These are current as of Python 3.5.0rc4, but could change.

### Use a different DLL

To specify your own path to the redistributable DLL, use a complete VS environment (such as the developer command prompt), set the environment variable `DISTUTILS_USE_SDK=1`, and then set environment variable `PY_VCRUNTIME_REDIST` to the full path of the DLL to use.

```
C:\package> "C:\Program Files\Microsoft Visual Studio 14.0\VC\vcvarsall.bat"
C:\package> set DISTUTILS_USE_SDK=1
C:\package> set PY_VCRUNTIME_REDIST=C:\Path\To\My\vcruntime140.dll
C:\package> python setup.py bdist_wheel
```

Note that this doesn't override any of the following steps, so if your version of Python already includes `vcruntime140.dll`, then it won't be copied. However, there's no reason you can't specify a different name here, and then it will be copied (but then you have to deal with a different name file, and might be missing actual dependencies... not really a great idea, but you can do it).

### Always statically link

The build options are updated to statically link the DLL if `PY_VCRUNTIME_REDIST` is **empty**.

If you set `DISTUTILS_USE_SDK` but not `PY_VCRUNTIME_REDIST`, you will get statically linked `vcruntime140.dll`. This is probably the biggest surprise from the change.

### Always dynamically link, but don't copy
Probably the most useful customisation, you can choose to dynamically link the DLL but not copy it to your build output.

Since we check the name already, any file named `vcruntime140.dll` will never be copied anyway. However, with a future compiler you may already know that you are distributing the correct files and do not need distutils to help.

First, you apply the opposite of the previous customization: build options are updated to dynamically link the DLL if `PY_VCRUNTIME_REDIST` is **not** empty.

However, the DLL is only copied to the output directory if `os.path.isfile` is `True`.

The tests are deliberately different. It means you can force dynamic linking without the copy by setting `PY_VCRUNTIME_REDIST` to anything other than a valid file:

```
C:\package> "C:\Program Files\Microsoft Visual Studio 14.0\VC\vcvarsall.bat"
C:\package> set DISTUTILS_USE_SDK=1
C:\package> set PY_VCRUNTIME_REDIST=No thanks
C:\package> python setup.py bdist_wheel
```

### No customisation

And of course, if you don't need to customise anything, you can just call `setup.py` directly and distutils will automatically use the latest available compiler.

# Summary

It's been a bumpy ride, especially over the last week, but we're on track to release Python 3.5.0 in a good state.

Using MSVC 14.0 and the UCRT, we have independence from the compiler version.

When a new version of MSVC is available, extensions built for Python versions that may not include the default dependencies will bundle them in their own package. (There are still some uninstallation concerns here to resolve, but we have time for those.)

In 10 years time it should still be possible to build extensions that work with Python 3.5.0 using the latest tools available at that time. Everyone who builds for Python 2.7 today should be excited by that prospect.

Thank you all again for everyone who has contributed testing, feedback, fixes and suggestions. We hope you will love Python 3.5.
