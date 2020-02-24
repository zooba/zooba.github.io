---
layout: post
title: Building Extensions for Python 3.5
slug: building-for-python-3-5
category: Python
tags:
- compiler
- crt
- msvc
- msvcrt
- python
- Windows
- ucrt
---

> Some parts of this post have been superseded. If you are interested in the background, continue reading (I have marked the parts that are now incorrect). If you simply want to see how Python 3.5 and later are avoiding CRT compatibility issues, you can jump straight to [part two](/blog/building-for-python-3-5-part-two/).

---

As you read through the [changelog](https://docs.python.org/3.5/whatsnew/changelog.html) or [What's New](https://docs.python.org/3.5/whatsnew/3.5.html) for Python 3.5, you may notice the following item:

> Windows builds now use Microsoft Visual C++ 14.0, and extension modules should use the same.

For most Python users, this will not (and should not) mean anything. It doesn't affect how you use or interact with Python, it isn't new syntax or a new library, and generally you won't notice any difference other than a small performance improvement (yay!).

However, Python [extenders and embedders](https://docs.python.org/3.5/extending/index.html) care a lot about this change, because it directly affects how they build their code. Since you are almost certainly using their work (numpy, anyone? Blender? [MATLAB](http://www.mathworks.com/help/matlab/call-python-libraries.html)?) you should really hope that _they_ care. However, this is a change, and no matter how good the end result is, change takes time. Please be patient with project maintainers, as they will have to spend more time supporting Python 3.5 than previous versions.

So while the most obvious benefit for most people may be a performance improvement, we haven't even bothered benchmarking this precisely. Why not? Because the long-term benefits of the change are so good it would be worth _sacrificing_ performance to get them. And we know it's going to hurt some project maintainers in the short term, and again, the long-term benefits for the entire ecosystem - and those same maintainers - are worth it.

As I'm largely responsible for the compiler change (with the full support of the CPython developers, of course), this post is my attempt to help our ecosystem catch up and set the context so everyone can see just how the benefits are worth the pain. [Python 3.5.0rc2](https://www.python.org/downloads/release/python-350rc2/) is already available with all of these changes, so project maintainers can be testing their builds now.

# First, some definitions

While this post is intended for advanced audiences who probably know all of these terms, I'll set out some definitions along the way just to make sure we're all talking about the same thing.

_MSVC_ is Microsoft Visual C++, the compiler used to build CPython on Windows. It's often specified with a version number, and in this post I'll refer to MSVC 9.0, MSVC 10.0 and MSVC 14.0.

_CRT_ refers to the C Runtime library, which for MSVC is provided and supported by Microsoft and contains all of the standard functions your C programs can call. This is a heavily overloaded term, so I'll be more specific and refer to DLL names (like `msvcr90.dll`) or import library names (like `libucrt.lib`) where it matters.

_MSVCRT_ refers specifically to the CRT required by MSVC. Other compilers like gcc have their own CRT, typically known as `libc`, and even MSVCRT is made up of parts with their own distinct names.

_VCRedist_ will come up later, and it refers to the [redistributable package](http://www.microsoft.com/en-us/search/result.aspx?q=vc%20redist&amp;form=DLC) provided by Microsoft that installs the CRT, as well as extra files required for C++ programs and Microsoft extensions such as [C++ AMP](https://msdn.microsoft.com/en-us/library/vstudio/hh265136.aspx) and the [Concurrency Runtime](https://msdn.microsoft.com/en-us/library/dd504870.aspx).

# MSVCRT's Little Problem

The problem is rooted in a design decision made many years ago as part of MSVC (I don't even know which version - probably the very earliest). While we can view it differently today, at the time it was clearly a good design. However, the long-term ramifications were not obvious without the rise of the internet.

Each version of MSVC (the compiler) comes with a matched version of the CRT (the library), and the compiler has intimate knowledge of the library. This allows for some cool optimizations, like choosing different implementations of (say) `memcpy` automatically based on what the compiler knows about the variables involved - if it can prove the ranges never overlap, why bother checking for overlap at runtime?

However, it does mean that when you use a different compiler, you also have to use the matched CRT version or everything breaks down very quickly. Generally this is okay, since when a developer upgrades to a newer compiler they can rebuild all of their code. The reason the internet causes this to break down is the rise of plugins and the ease of updates.

Many applications support plugins that are loadable shared libraries (DLLs, often with a special extension such as `.pyd`). While the application may not consider or describe these as plugins - Python prefers "extensions" or "native modules" - it is still a plugin architecture. And with the internet, we have easier access than ever to download and install many such plugins, and also to update the host application.

The CRT comes into play because it is _shared_ between the host application and every plugin. Or rather, it _assumes_ that it is shared. Because of the way Windows loads DLLs, if the host application and all its plugins are built with the same MSVC version and hence use the same CRT version, the state kept within that CRT would be shared.

Shared state includes things such as file descriptors, standard input/output buffering, locale, memory allocators and more. These features can be used equally by the host and its plugins without conflicts resulting in data corruption or crashes.

However, when a plugin is built with a _different_ CRT, this state is no longer shared. File descriptors opened by the plugin do not exist (or worse, refer to a different file) in the host, file and console buffering gets confused, error handling is no longer synchronised, memory allocated in one cannot be freed in the other and so on. It is _possible_ to safely use a plugin built with a different CRT, but it takes care. A lot of care.

This is the situation that Python 2.7 currently suffers from, and will continue to suffer from until it is completely retired. Python 2.7 is built with MSVC 9.0, and because of compatibility requirements, will _always_ be built with MSVC 9.0 - otherwise a minor upgrade would break all of your extensions simultaneously, including the ones that nobody is able to build anymore.

Unfortunately, MSVC 9.0 is no longer supported by Microsoft and all the free downloads were removed, making it essentially impossible to build extensions for Python 2.7. The easiest mitigation was to keep making the compilers available in an unsupported manner, so [we did that](http://aka.ms/vcpython27), but it still leaves projects in a place where they are using old tools, likely with unpatched bugs and vulnerabilities. Not ideal.

Python 3.3 and 3.4 were built with MSVC 10.0, which is in essentially the same position. The compiler is no longer supported and the tools are no longer easily available. Building extensions with later versions of MSVC results in CRT conflicts, and building with the older tools misses out on security fixes and other improvements.

> One example of an improvement in MSVC 14 that is not in MSVC 10 or earlier is support for the C99 standard. I'm not claiming it's 100% supported (it's not), but even 90% support is much more useful than what was previously available.

The best mitigation we have for MSVC 10.0 builds of Python is to migrate to Python 3.5. Luckily, doing so does not require the same porting effort as moving from Python 2.7 would require, but it raises the question: why is Python 3.5 any better?

The answer is: UCRT.

# The UCRT Solution

As part of Visual Studio 2015, MSVCRT was [significantly refactored](http://go.microsoft.com/fwlink/?LinkID=617977). Rather than being a single `msvcr140.dll` file, as would be expected based on previous versions, it is now separated into a few separate DLLs.

The most exciting one of these is `ucrtbase.dll`. Look carefully - there is no version number in the filename! This DLL contains the bulk of the C Runtime and is not tied to a particular compiler version, so plugins that reference `ucrtbase.dll` will share all the state we discussed above, even if they were built with different compilers.

Another great benefit is that `ucrtbase.dll` is an [operating system](http://www.microsoft.com/en-us/download/details.aspx?id=48234) component, installed by default on Windows 10 and coming as a recommended update for earlier versions of Windows. This means that soon _every_ Windows PC will include the CRT and we will not need to distribute it ourselves (though the Python 3.5 installer will install the update if necessary).

> It's very important to clarify here that the compatibility guarantees only hold when linked through `ucrt.lib`. The public exports of `ucrtbase.dll` may change at any time, but linking through `ucrt.lib` uses [API Sets](https://msdn.microsoft.com/en-us/library/windows/desktop/hh802935.aspx) to provide cross-version compatibility. Using the exports of `ucrtbase.dll` directly is not supported.

So the major issue faced by earlier versions of Python no longer exist. The next version of MSVC will be able to build extensions for Python 3.5, and it may even be possible for later version of Python 3.5 to be built with newer compilers without affecting users. But while this is the start of the story, it isn't the end and the rest is not so pretty.

## The UCRT Problems

While `ucrt.lib` is a great improvement over earlier versions, if you followed the link above or just read my comment carefully, you'll see the rest of the problem. Besides `ucrtbase.dll`, there are other libraries we need to link with.

For pure C applications, the other DLL we need is `vcruntime140.dll`. Notice how this one _includes_ a version number? Yeah, it depends on the version of the compiler that was used. Applications using C++ will likely depend on `msvcp140.dll`, which is also versioned. We have not yet completely escaped DLL hell.

Why weren't these libraries also made version independent? Unfortunately, there are places where the compiler still needs intimate knowledge of the CRT. They are very few, and `vcruntime140.dll` in particular exports almost no functions that are both documented and have no preferred alternative in `ucrtbase.dll` (for example, `memcpy` may be used from `vcruntime140.dll`, but `memcpy_s` from `ucrtbase.dll` should be preferred). However, much of the critical startup code is part of `vcruntime140.dll`, and this is so closely tied to what the compiler generates that it cannot reasonably be made compatible across versions.

Ultimately, depending on any version-specific DLL takes us right back to the earlier issues. Extensions for Python 3.5 will need to use MSVC 14.0 or else include the version-specific DLLs - Python 3.5 _could_ include `vcruntime140.dll`, but if an extension depends on `vcruntime150.dll` then it is not easily distributable.

Luckily, this concern was raised as the UCRT was being developed, and so there is a semi-official solution for this that happens to work well for Python's needs.

---

## The End (of part one)

> Remember how I said at the start that some of this blog is no longer valid for Python 3.5? Yeah, that's from here to the end. To see what we've actually done, stop reading here and read [part two](/blog/building-for-python-3-5-part-two) instead.

---

## The Partially-Static Solution

To avoid having a runtime dependency on `vcruntime140.dll`, it is possible to statically link _just that part_ of the CRT. Effectively, the required functions, which tend to be a very small subset of the complete DLL, are compiled into the final binary. However, the functions from `ucrtbase.dll` are still loaded from the DLL on the user's machine, so many of the issues associated with static-linking are avoided.

There are many downsides to static linking, especially of the core runtime, ranging from larger binaries through to not automatically receiving security updates from the operating system. Previously, applications including Python have avoided static linking by distributing the CRT as part of the application ("app local"), but while this avoids some of the bloat concerns, the application distributor is still responsible for providing updates to the CRT. Statically linking `vcruntime140.dll` also leaves responsibility with the distributor for some updates, but significantly fewer.

> **Warning:** This is where things get technical. Skip to the next section if you just want to know what you'll need to fix.

The difference between dynamic linking and static linking is based on a few options passed to both the compiler (`cl.exe`) and the linker (`link.exe`). Most people are familiar with the compiler option, one of `/MD` (dynamic link), `/MDd` (debug dynamic link), `/MT` (static link) and `/MTd` (debug static link). As well as automatically filling out the remaining settings, these also control some code generation at compile time - different code needs to be compiled for static linking versus dynamic linking, and this is how that option is selected at compile time.

For the linker, there are separate libraries to link with. If the compiler option is provided, these are selected automatically, but can be overridden with the `/nodefaultlib` option. This table is adapted from the [VC Blog](http://blogs.msdn.com/b/vcblog/archive/2015/03/03/introducing-the-universal-crt.aspx) post I linked above:

```
Release DLLs   (/MD):
    msvcrt.lib   vcruntime.lib      ucrt.lib
Debug DLLs     (/MDd):
    msvcrtd.lib  vcruntimed.lib     ucrtd.lib
Release Static (/MT):
    libcmt.lib   libvcruntime.lib   libucrt.lib
Debug Static   (/MTd):
    libcmtd.lib  libvcruntimed.lib  libucrtd.lib
```

> I will ignore the debug options for the rest of this post, as debug builds should generally not be redistributed and can therefore reliably assume all the DLLs they need are available. This is why the Python 3.5 debug binaries option requires Visual Studio 2015 - to make sure you have the debug DLLs.

For a fully dynamic release build, we've built with `/MD`. This enables codepaths in the CRT header files that decorate CRT functions with `declspec(dllimport)` and so code is generated for calls to go through an import stub. Linking in `vcruntime.lib` and `ucrt.lib` provides the stubs that will be corrected at load time to refer to the actual DLLs.

For a fully static build, we use `/MT` which omits the `declspec`'s and generates normal `extern` definitions. Linking with `libvcruntime.lib` and `libucrt.lib` provides the actual function implementation and the linker resolves the symbols normally, just as if you were calling your own function in a separate `.c` file.

What we want to achieve is linking with `libvcruntime.lib` for the static definitions, but `ucrt.lib` for the import stubs. Unfortunately, the compiler does not know how to generate code for this case, so it will either assume import stubs for all functions, or none of them, which results in linker errors later on.

There is one case that works: if we compile with `/MT` so the CRT will be statically linked, the generated code assumes everything can be resolved through it's regular name. When linking, if we then substitute `ucrt.lib` instead of `libucrt.lib`, the _linker_ can generate the import stubs needed to call into the DLL.

The build commands look like this:

```
cl.exe /MT /GL file.c
link.exe /LTCG /NODEFAULTLIB:libucrt.lib ucrt.lib file.obj
```

We use `/MT` to select the static CRT. The `/GL` and `/LTCG` options enable link-time code generation, and the `/NODEFAULTLIB:libucrt.lib ucrt.lib` arguments ignore the static library and replace it with the import library. The linker then generates the code needed for this to work, and we end up with a DLL or an executable that only depends on `ucrtbase.dll` (via the API sets).

Unfortunately, there are some follow-on effects because of this change.

# What else does this break?

> In case you skipped the last warning, the rest of this post is now invalid. To see what we've actually done, stop reading here and read [part two](/blog/building-for-python-3-5-part-two) instead.

---

With Python 3.5, `distutils` has been updated to build extensions in a portable manner by default. Most simple extensions will build fine, and your wheels [can be distributed](https://packaging.python.org/en/latest/) to and will work on any machine with Python 3.5 installed. However, in some cases, your extension may fail to build, may produce a significantly different `.pyd` file from previously, or may need extra dependencies when distributed.

## Static Libraries

The first likely problem is linking static libraries. Because of the compiler change, you will probably need to rebuild other static libraries anyway, and it is important that when you do you select the _static CRT_ option (`/MT`). As discussed above, we don't actually link the entire CRT statically, but if your library expects to dynamically load the CRT DLL then it will fail to link.

If your library requires C++, your resulting `.pyd` _will_ statically link any parts of the C++ runtime, and so it may be significantly larger than the same extension for Python 3.4. This is unfortunate, but not a critical issue, and it actually has the benefit that your extension will not be interfered with by other extensions that also use C++.

Of course, in some cases you _really_ do not want to do this. In that case, I would _strongly_ discourage you from uploading your wheels to [PyPI](https://pypi.python.org/pypi), since you will also need to get your users to install the VCRedist version that matches the compiler you used. Currently, there is no way to check or enforce this through tools like pip.

Since it is so strongly discouraged, I'm not even going to show you how to do it, though I'll give basic directions. In your `setup.py` file, you'll want to monkeypatch `distutils._msvccompiler.MSVCCompiler.initialize()` (yes, the underscore in `_msvccompiler` means this is not supported and we may break it at any point), call the original implementation and then replace the `'/MT'` element in `self.compile_options` with `'/MD'`.

Ugly? Yep. By going down this path, you are making it near impossible for non-administrative users to use your extension. Please be very certain your users will be okay with this.

## Dynamic Libraries

If you have binary dependencies that you can't recompile but have to include, then your best option is to include the redistributable DLLs alongside them. Test thoroughly for CRT incompatibilities, especially if your dependencies use a different version of MSVCRT, and generally assume that only `ucrtbase.dll` will be available on your user's machines. [Dependency Walker](http://dependencywalker.com/) is an amazing tool for checking binary dependencies.

## Incompatible Code

The third likely issue that will be faced is code that no longer compiles. There has been an entire deprecation cycle between MSVC 10.0 and MSVC 14.0, which means some functions may simply disappear without warning (because the warning was in MSVC 11.0 and MSVC 12.0, which Python never used). There have also been changes to unsupported names and a number of non-standard names are now indicated correctly with a leading underscore.

Also, as with every release, the graph of header files may have changed, and so names that were implicitly `#include`d previously may now require the correct header file to be specified. (This is not necessarily names moving into different header files, rather, one header file may have included another and the name was available that way. Dependencies within header files are not guaranteed stable - you should always include all headers directly when you require their definitions.)

A lot of code tries to fill gaps in various compilers and runtimes by defining functions under `#ifdef` directives. With the range of changes that have occurred, most of these should be checked and updated - `_MSC_VER` is defined as at least `1900` now, and because of the switch to `/MT` some defines of CRT exports may need to have the `declspec(dllimport)` removed (or remove the entire declaration and use the official headers).

## MinGW

Finally, extensions that are built with gcc under MinGW are likely to have [compatibility issues](http://bugs.python.org/issue4709) for some time yet, since the UCRT is not a supported target for those tools. Again, this pain is unfortunate, but long term it should be entirely feasible for the MinGW toolchain to support the Universal CRT better than the MSVC 10.0 CRT.

# In Summary

> If you made it this far, _technically_ this part is still mostly correct. But then, it doesn't really add anything new. To see what we've actually done, read [part two](/blog/building-for-python-3-5-part-two).

---

By moving Python 3.5 to use MSVC 14.0 and the Universal CRT, we have (hopefully) removed the restriction to build extensions with matched compilers.

Extensions built with `distutils` can be distributed easily, though they may be larger or have more build errors as a result of different build settings.

Long term, we believe this change will avoid the problems currently faced by those building for Python 2.7 as toolchains are deprecated and retired.

The short-term pain we are going to experience would have occurred for any compiler change, but after this we should be largely insulated against the next.

Finally, please show some respect and grace towards the maintainers of projects you depend upon. It may take some time to see fully compatible releases for Python 3.5, and shouting at or abusing people online is not necessary or even helpful.

I personally want to thank everyone who distributes builds of their packages for Windows, which they don't strictly need to do, and I apologise for the pain of transition. This change is meant to help you all, not to hurt you, though the benefits won't be seen for some time. Thank you for your work, and for making the Python ecosystem _exist_ for millions of users.
