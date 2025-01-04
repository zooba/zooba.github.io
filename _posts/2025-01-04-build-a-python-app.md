---
layout: post
title: Building a Python App
slug: build-a-python-app
category: Python
keywords:
- python
- Windows
---

Normally when you go to run a Python app, you'll be thinking about opening a terminal and typing the command `python`, followed by whatever app you want.

You're probably aware that most of your other apps don't behave like this. They have icons, and you can hit "Start" and search for them, and you don't need to remember to activate their environment.

How come Python is so different? And why can't we have all the same features for a Python app?

It turns out, of course, that we can.

<!--more-->

However, it involves doing a bit more work than usual.

Let's say you've already written the application and put it on PyPI as a wheel. You tell your users to create a virtual environment and run specific commands to install and launch it. Let's say you used [Rich](https://pypi.org/project/rich/), which is a beautiful library for making console UI, so that users don't have to remember command line arguments. They just run it and use it. Maybe it manages their music, or video files, or whatever stuff people manage these days when they aren't just streaming it.

In short, you've built an app.

It doesn't _actually_ need a Python environment for anything except itself. It doesn't _actually_ need users to learn about environment variables and shell quoting and to change their security settings to allow scripts. It doesn't _actually_ need users to find and install a specific version of Python and then configure their environment and then create a virtual environment and then configure their package manager and then open the right shell and so on and so on.

They only need this because you gave a [kit car](https://en.wikipedia.org/wiki/Kit_car) instead of a ready-to-run app. You've forced your user to use developer tools.

There is a better way.

> Side note: I'm going to go into detail on doing Python apps _for Windows_, because that's my specialty. You'll have to find a different specialist for other platforms, though conceptually there are a lot of similarities.

The better way is to make your users _an installer_.

Yes, this is going to involve some work. From you, not them. There isn't a magic wand, or rather, of the multiple magic wands we need, most of them are kit cars.

If you've given up already, I understand. You don't actually _have_ to care enough to do the work for your users. And there are other ways that seem like shortcuts that you'll probably want to try first. Go ahead. This post will still be here when you haven't been able to make your app as accessible as it could be.

Still with me? Okay, here's what we're going to do:

* Create a launcher executable for our app
* Create the layout for our app
* Package the app into an installer

Sounds simple enough? Great, let's go.

# Create a launcher executable

As a Python developer, you'll be familiar with `python.exe`. This is what you'd normally tell users to run, but we don't want that - we want to tell them to run `MyAwesomeApp.exe`, but we _don't_ want `MyAwesomeApp.exe` to be regular Python. If someone types `MyAwesomeApp.exe some_script.py` then it should run _our app_, not that random script.

So we're going to rewrite `python.exe` from scratch, but make it do things our way.

If that seems intimidating, then go check out [the source code for `python.exe`](https://github.com/python/cpython/blob/main/Programs/python.c). I'll wait.

Okay, I waited long enough, and most of you didn't click anyway. So here's the source code:

```c
int wmain(int argc, wchar_t **argv)
{
    return Py_Main(argc, argv);
}
```

One line. It turns out `python.exe` doesn't really do much, and all the functionality is actually in `python313.dll`. (For the sake of this post, I'll use Python 3.13 as the example, but swap it to the later version if you're from the future.)

Now, `python313.dll` contains _a lot_ of code. We're not going to rewrite it. What we're going to do is to _embed_ it.

Embedding is when you take an existing app (or in this case, a new one), written in native code, and _load_ CPython. This gives you much more control over the entire interpreter than launching `python.exe`. And we're going to use that control.

Let's start with the code and walk through it. This is your entire launcher executable, call it `main.c` or something like that:

```c
#include <Python.h>

int wmain(int argc, wchar_t **argv)
{
    PyStatus status;
    PyConfig config;
    PyConfig_InitIsolatedConfig(&config);
    status = Py_InitializeFromConfig(&config);
    if (PyStatus_Exception(status)) {
        PyConfig_Clear(&config);
        if (PyStatus_IsExit(status)) {
            return status.exitcode;
        }
        Py_ExitStatusException(status);
        return -1;
    }

    // CPython is now initialised

    int exitCode = -1;
    PyObject *module = PyImport_ImportModule("MyAwesomeApp");
    if (module) {
        // Pass any more arguments here
        PyObject *result = PyObject_CallMethod(module, "main", NULL);
        if (result) {
            exitCode = 0;
            Py_DECREF(result);
        }
        Py_DECREF(module);
    }
    if (exitCode != 0) {
        PyErr_Print();
    }
    Py_Finalize();
    return exitCode;
}
```

As you can see, it's all in your `main` function. Feel free to refactor. The first part, up to the comment, is pretty boilerplate. For simple cases like this, we don't have to do much to initialise Python, though it _is_ important that we use the `PyConfig_InitIsolatedConfig` function to generate our settings.

Once initialised, we're going to use CPython's C API to import our `MyAwesomeApp` module (a Python module, probably a directory called `MyAwesomeApp` with an `__init__.py` file inside), and then call its `main()` function. There's a bit of error handling and reference counting, but basically the equivalent Python code would be:

```
import sys
import traceback
try:
    import MyAwesomeApp
    MyAwesomeApp.main()
    sys.exit(0)
except:
    traceback.print_exc()
    sys.exit(-1)
```

Notice that we don't have to set up any library paths or anything? That'll be handled naturally when we set up our layout. However, if you were to change the layout in some ways you may

We also don't pass any arguments to our `main()` function. If you needed them, you'd update the [`PyObject_CallMethod`](https://docs.python.org/3.13/c-api/call.html#c.PyObject_CallMethod) call. Note that `sys.argv` doesn't get initialised here - that wouldn't be very "isolated" - so if you want to pass command line arguments then you'll be doing a bit of converting.

To compile this file, you can use any tool or compiler you like, provided it can use the CPython headers and import libraries. On the command line using Microsoft Visual C++, the commands will look like this:

```
> cl /c main.c /I<path to Python includes> /Fo:main.obj
> link main.obj /LIBPATH:<path to Python libs> /OUT:MyAwesomeApp.exe
```

Remember the version of Python you used for the includes and libs! We're going to have to stick with that version for the rest of this process. (One of the biggest benefits of distributing an app rather than a wheel is _you only need to support one version of Python_! And you get to choose which one!)

And make sure you get your architectures to match - a 64-bit Python needs 64-bit compilers and linkers!

> Adding icons, version info, display names, and code signing are additional steps that you are likely to want to do. But you can find that information elsewhere - I'm focusing on the Python-specific pieces right now.

Now if you run `MyAwesomeApp.exe`, you get... nothing.

Huh?

Diagnosing this problem could take some time, so I'll save a few steps. The exit code of your app was `-1073741515`, or more readably, `0xC0000135`. This is the error you get when a DLL your app relies on cannot be found. In this case, it's because it doesn't actually have `python313.dll` available. So let's go get it!

# Create a Python app layout

The app _layout_ is basically all the right files in the right place. CPython on Windows is relocatable, which means you can simply copy its files around and it will run (provided you copy the _right_ files). The same applies to other libraries you may be used.

> If you've heard people say that you can't just copy Python and its packages around, recognise that they aren't talking about Windows. It's true that Linux users don't get this feature (by default), and so they resort to Docker and other forms of containers, but on Windows it actually just works.

We'll start by setting up a layout directory. It'll be empty, and can be anywhere. I just mention it here because I'm going to say "into the layout" quite a few times ahead, and wanted to be clear that when I say that, I mean this directory.

The pieces we need to bring together are our app's own files, all our dependencies (other Python packages that we use), a copy of Python itself, and the launcher we just built above. If your app is already on PyPI then you could treat it like any other dependency, or you can just copy it directly into place in the layout.

For installing dependencies, we're going to use `pip` from our existing Python install. Other tools besides `pip` are available, _but most do not install properly_ for what we're doing here. We don't want automatic environments or secret caches, we just want the actual files.

We'll use `pip`'s `--target`` option to install the packages into our layout directory. This may seem unusual, and some documentation may warn against using this option, but it's intended precisely for this job. As long as the directory is currently empty, it'll be fine.

```
> pip install MyAwesomeApp --target <layout directory>
```

All going well, you've now got a directory containing your app and all its dependencies. Perfect!

Next, we need a Python runtime. You can use any install of Python and copy it over, however, the _embeddable package_ available from [python.org downloads](https://www.python.org/downloads/windows/) is pre-prepared for this kind of job. So we'll grab that - make sure you get the same version and architecture as what you've been using!

We'll just extract it directly into the layout directory. It's safe to delete `python.exe` and any `.pyd` or `.dll` files you aren't using, but leave the rest alone. The most magic file is `python313._pth`, which is plain text and can be read easily:

```
python313.zip
.

# Uncomment to run site.main() automatically
#import site
```

This file lists the contents of our `sys.path` - that is, the directories from which Python is going to import files. Its filename matches the DLL, which means it will always be used whenever we load Python from our app's directory. The default contents will search the `python313.zip` file (which contains the standard library, and should be left zipped up) as well as the current directory, which at this point also contains our dependencies. In this case, we don't need to modify `python313._pth` at all, but if we wanted to put our Python code in a different directory, we would have to update it to include that path.

Finally, we want to copy `MyAwesomeApp.exe` that we built previously into the layout directory. _Now_ when we run it, it will import and launch our application. And if we want more commands, it's really easy to build another executable and copy it in.


# Packaging the app

If we wanted to, it would be just fine to zip up the layout directory and share it as a `.zip` file. A lot of users are quite happy with this, though it does make it fairly likely that your app will just be extracted in a Downloads folder and get into all sorts of trouble.

You might consider creating an MSIX package, which is the modern installer format used by Windows. These packages can be uploaded to the Windows Store (if you have a publisher account), or signed and distributed directly to Windows 11 machines (if you have a code signing certificate). MSIX packages can define Start menu icon, file associations, and more. The [Microsoft documentation for packaging apps](https://learn.microsoft.com/windows/uwp/packaging/) is a good starting point.

Alternatively, to create an old-style MSI, you probably want to look into [WiX Toolset](https://github.com/wixtoolset). This is likely to be more work, but it gives you access to a huge amount of functionality to do anything you may need when setting up your app on a user's machine.

There are plenty of other tools for creating installers, too, and virtually any of them will work. Just remember that you really don't need to do anything other than copy your layout directory onto a user's machine. It can be easy to get distracted by some of the "automatic" features offered by these tools, but we've already done the hard work and they're only likely to spoil things.


# What Next?

This is a very _very_ basic intro to turning your Python runnable package into an actual app. Virtually every point I mentioned can branch off into an incredible amount of depth, whether that's defining resources for your launcher executable, adding file associations to your installer, reducing security risks by tweaking your layout, or automating the build with scripts.

If you want to check out a more complex project that uses this approach, you can see my [reference implementation](https://github.com/zooba/pymanager) of a potential new Python install manager - itself a Python app. It has both MSIX and MSI packaging, and is largely scripted using [`pymsbuild`](https://github.com/zooba/pymsbuild), my build backend that has some features like automatically generating (basic) launcher executables and packing your own Python code into a DLL so that it can't be read or modified easily (but more importantly, so that it loads _much_ faster).

Running into issues or have ideas for making app redistribution better? Come and join the discussion [on the Python forums](https://discuss.python.org/c/packaging/14).

_Comments and discussion [on X](https://x.com/zooba/status/1875633314343874692)_
