---
layout: post
title: Debugging Cython with WinDBG
slug: debugging-cython-with-windbg
categories: Python
tags: cython,debugging
---

This is one of those "note to self" kinds of blogs. I spent <em>just</em> enough time figuring this out right now that I don't want to repeat, so here's a problem that I hit and the solution. Hopefully it helps someone else besides future-me.

# The Problem

I have a file of Cython code, similar to, but not actually my [winhttp](https://github.com/zooba/winhttp) project. And when it's all built and run, it simply crashes. Exit code 0xC0000005 at address 0x11, which is the best kind of error to get (spoiler: it's not the best kind of error).

<!--more-->

So as is normal for me (spoiler: I'm not normal), I pull out WinDBG, specifically [WinDBG from the Microsoft Store](https://www.microsoft.com/p/windbg-preview/9pgjgd53tn86), which for Windows 10 is a much nicer experience than the traditional one. Unfortunately, Cython by default does not produce good debugging information, so my native call stack is horrendous and I'm no closer to finding the problem.

# Setup.py: Before

Here's the relevant part of my setup.py file before updating:

```python
EXT_MODULES = cythonize([       
    Extension("_winhttp", ["_winhttp.pyx"])
])
```

This is a pretty standard way of building a Cython module. But it enables no debugging support.

# Setup.py: After

Here's the same part of my setup.py after updating for debugging. This is not something I'd check in (or it'd be under a special flag), but it helped me get to a point where WinDBG could show me useful information.

```python
EXT_MODULES = cythonize([       
    Extension("_winhttp", ["_winhttp.pyx"],
              extra_compile_args=["-Ox", "-Zi"],
              extra_link_args=["-debug:full"])
], emit_linenums=True)
```

Let's look at the four additions here:
* "-Ox" is the MSVC compiler argument for disabling all optimizations. While we don't want to switch entirely into a debug build (which results C runtime conflicts), disabling optimizations will make it easier to debug.
* "-Zi" is the MSVC compiler argument to generate full debug information. This is needed for the linker to produce a PDB file
* "-debug:full" is the linker argument to use the debug information to produce the PDB file.
* "emit_linenums" is a semi-secret Cython option that adds "#line" directives throughout the generated C/C++ code. These instruct MSVC to generate a source map back to the original Cython file.

With all of these options enabled, when running my code under WinDBG, it will break at the correct location and show a valid callstack that maps back to my .pyx file. Unfortunately, Cython's variable name mangling means that it's still a little bit of work to match locals to source code, but otherwise it makes debugging your Cython extension significantly easier.

<img src="/assets/2019-05-17-callstack.png" alt="Cython code with line highlighting and call stack in WinDBG" />
