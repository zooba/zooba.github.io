---
layout: post
title: The most critical Python code metric
slug: most-critical-python-metric
category: Python
keywords:
- design
- design-patterns
- language
- metrics
- python
---

Code metrics are a popular way to analyse the complexity of our software. For some reason, we are attracted to single-figure summaries of quality, whether it's lines of code, [cyclomatic complexity](https://wikipedia.org/wiki/Cyclomatic_complexity), or the [Pylint score](http://pylint.pycqa.org/en/latest/user_guide/output.html#score-section).

Personally, I think using these are as valuable as judging another person based on one of their visible or measurable characteristics.

Which is to say, an okay metric might get the conversation started, but it won't help you _know_ them. That takes time and effort.

Comparing measurements is also just as ridiculous with code as it is with people, _except_ when measuring your own progress. If you know which direction is "better", then you can tell whether a change moved in that direction.

In that light, my favourite metric for Python code is "area under the indent" (less is better).

![Area under the indent example](/asserts/AreaUnderIndent.png)

This measurement (or estimate) captures both lines of code and nesting depth, which is more valid for Python than other languages because of Python's one-effect-per-line style. If you enforce a reasonable line length limit, then you'll also prevent people packing extremely long expressions into one line in order to game the metric.

But that's not the point of this post.

Having a simple, comparable metric doesn't help you write better code. Knowing a person's gender, weight or 5km time doesn't help you respect them. Something more is required.

---

Another thing I am often asked for is Python design patterns. Typically by Java developers, who are used to having well written architectural idioms, and are thrown by Python's more relaxed approach.

In response, I don't offer them specific advice. I give them a simple question, which, applied anywhere throughout your entire codebase, leads to the most well designed code.

And as it happens, well-designed code fares well under most complexity metrics (except for "source code bytes on disk", but those are cheap).

The question is... how am I going to test this?

# How will I test this?

Different languages take different approaches to validating code correctness. Many start with static typing (C++) and some end there (Java). Others extend it further (Ada) and some even further ([SPARK](https://en.wikipedia.org/wiki/SPARK_%28programming_language%29)), while some rely entirely on the skill of the developer (C).

Python has always relied on dynamic testing: to know whether your code is correct, you should run it and see what it does!

This has huge value in terms of expressiveness. It enables duck-typing, metaclasses, and decorators, all of which are difficult or impossible to verify statically. It enables runtime code generation and import search path manipulation. It enables embedding into other applications. It enables a range of non-breaking API changes that would be fatal in other languages.

The cost is that you have to write tests.

Thirty years ago, when Python was invented, I'm sure this seemed an unnecessary burden. These days, engineering best practices recommend tests for all code, regardless of language.

But rather than being a burden, writing tests for dynamic code is actually the best way to ensure your architecture is well designed. Not because the tests themselves are magic, but because they interact with your source code from a perspective that essentially forces you into good design.

# Well Designed Architectures

To set some context, here are a few of my characteristics of well-designed architectures. (You may disagree with these, in which case I will assume you have poorly designed software.)

Easy to navigate
: You can easily find a piece of code based on knowing what it does

Easy to modify
: You can easily change a piece of code without impacting things that use it

Easy to identify side effects
: You can easily tell whether a piece of code requires/modifies external state

Easy to avoid side effects
: You can use part of your code and deliberately bypass any external impact

The following sections will briefly describe what I mean by these characteristics, and suggest how writing tests will help you achieve them.

There's a noticeable lack of code examples, because I'm not trying to _teach_ anything here, merely to provoke deep(er) thoughts. With a bit of luck, some readers will find themselves inspired to take these ideas and present them in a form that's accessible to more people.

# Easy to navigate

We spend much more time reading code than writing it. _Far_ more time. So much so that it's a little disappointing how much we invest in coding tools compared to reading tools, but that's a topic for another rant...

Often, you are reading your code because it is doing something wrong. This implies that you know what it ought to be doing, and so are reading with the intent to locate a specific part of the code. Once you locate that piece of code, you'll want to understand it (so you can fix it), so hopefully it is small enough to fit in your short-term memory.

Designing for unit tests provides everything we're hoping for. If you have a long function that does multiple things, you can't write a unit test for it (since it represents multiple units), so you'll have to refactor it into a sequence (or chain) of functions (or methods).

Once refactored into separate, unit-sized functions, you've now given yourself "headings" for each piece of code. Headings are _much_ easier to navigate than a single paragraph of text.

It's up to you, but if each function has a name that represents what it does, it's like you've given it a sensible heading. And if each function has a coherent, "works/doesn't work" step of the overall functionality, the name will be easy, and the place(s) it is called from will be simple sequences of function calls.

When you need to find that functionality later, rather than it being in the middle of a run-on paragraph of code, it will be in a nice, bounded, labelled, unit-testable block.

# Easy to modify

Once you've found the piece of code, you likely want to modify it. This means understanding where it fits in the overall sequence, what other components produce its inputs, and which depend on its outputs and side effects.

Assuming you have already broken up your monolithic functions into coherent chunks of functionality, the next step in making them testable is to be clear about the inputs and outputs.

We'll deal with side effects later, which means right now we are concerned with functions that return values and/or mutate arguments. The former are easy to test, and the latter are perfect for [`unittest.mock`](https://docs.python.org/3/library/unittest.mock.html).

Your goal in testing function inputs and outputs is to _exhaustively_ prove that the _behaviour has not changed_.

Easily modifying code means being able to quickly detect when you've accidentally altered the behaviour. So if it's tested exhaustively, it's easier to modify. But even if the tests are not exhaustive, the design implications are useful.

Functions that have complex interactions between arguments, unpredictable results, or too many arguments are not just difficult to tets. They're difficult to read, understand, use, and modify.

Imagine your function takes eight `True`/`False` flags. That's 256 combinations you need to test, not including genuine error cases. Depending on your patience, you might get through the first ten or twenty before deciding to refactor the function.

Or maybe your function ends by updating a parameter's attribute when it could return the calculated result. Each test is now three steps (create object, call function, check object) instead of just checking the function result.

If it's painful to write tests for your functions, it will be painful to modify them as well.

# Easy to identify and avoid side effects

Side effects are simultaneously the biggest challenge in software development, and also the entire point of it. While once upon a time producing a single result was a worthwhile benefit of a computer (person or device), today we rely on multiple asynchronous outputs from any computation, as well as ambient inputs from external data sources.

Tests dislike side effects. They _really_ dislike side effects. Code with side effects implicitly has ordering constraints, timing constraints, complex setup and teardown processes, single threadedness, and may require additional or platform-specific hardware.

And unsurprisingly, humans also dislike side effects. When reading code, you have to keep an abstract state machine running inside your head to track what each variable currently holds so you can figure out which operations will do what. Each external source of data (reading from a file, for example) resets your in-brain state to "I don't know". Each external action (opening file) is an operation that resets your in-brain sequence to "I don't know".

Side effects spoil our ability to reason about how code will be executed.

So when you are architecting your code for testing, you're going to try and isolate side effects from computation. Trying to test a function that adds a list of numbers from a file? Refactor it into a function that opens and reads the file, and a function that adds the numbers. Now you can test the addition function without needing a file system.

Copying a set of files from one location to another? Collect all the metadata about those files in one function, calculate which ones need to be copied in the next, and then do the actual copying in the third function.

This *read*->*transform*->*apply* sequence clarifies which external state is used and where. It forces you to give each function useful names, and determine the structure of data passed between them.

You can now test the transformation on fake data, as exhaustively as you like, without any of the constraints imposed by side effects.

You can more easily find, understand, and fix the code based on the problem description, and debug issues by comparing your function's inputs and outputs against the system's reality.

The transform logic is more portable, as all platform-specific code is in the reading or applying stages. And your unit test matrix is narrower, even as you test more functions, because each is more focused. One edge case on one platform impacts one test, not your entire suite.

Most other side effects look very much like external state. Updating a field on some global object? Sending a progress message? Checking a configuration option? Make them inputs to your functions - a callback function (`tell_user`) is better than a global name (`print`), _even if the default argument is actually just the global_ (`tell_user=print`).

One day, you'll need to capture or suppress side effects for testing. The next, you'll want to find the code that causes a particular side effect. In both cases, you will benefit from having made them explicit parts of your interface.

# Summary

While easily-measured metrics are a popular way to evaluate code complexity, they can never give you the full story.

Various languages claim to be control complexity through fixed code styles, flexible code styles, static typing, dynamic typing, nominal typing, structural typing, separate interface definitions, converged interface definitions, significant whitespace, insiginificant whitespace, and more.

Regardless of how much code you have or the style it's been written in, if it's easy to navigate, easy to modify, and easy to identify and avoid side effects, you've likely got complexity under control.

And the easiest way to control complexity in Python is to write tests. Not because test coverage magically results in better code, but because architecting your code to be testable is the same as architecting for humans.
