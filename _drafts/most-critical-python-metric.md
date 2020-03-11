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

![Area under the indent example](TODO)

This measurement (or estimate) captures both lines of code and nesting depth, which is more valid for Python than other languages because of Python's one-effect-per-line style. If you enforce a reasonable line length limit, then you'll also prevent people packing extremely long expressions into one line in order to game the metric.

But that's not the point of this post.

Having a simple, comparable metric doesn't help you write better code. Knowing a person's gender, weight or 5km time doesn't help you respect them. Something more is required.

---

Another thing I am often asked for is Python design patterns. Typically by Java developers, who are used to having well written architectural idioms, and are thrown by Python's more relaxed approach.

In response, I don't offer them specific advice. I give them a simple question, which, applied anywhere throughout your entire codebase, leads to the most well designed code.

And as it happens, well-designed code fares well under most complexity metrics (except for "source code bytes on disk", but those are cheap).

The question is... how will I test this?

# How will I test this?

Different languages take different approaches to validating code correctness. Many start with static typing (C++) and some end there (Java). Others extend it further (Ada) and some even further ([SPARK](https://en.wikipedia.org/wiki/SPARK_%28programming_language%29)), while some rely entirely on the skill of the developer (C).

Python has always relied on dynamic testing: to know whether your code is correct, you should run it and see what it does!

This has huge value in terms of expressiveness. It enables duck-typing, metaclasses, and decorators, all of which are difficult or impossible to verify statically. It enables runtime code generation and import search path manipulation. It enables embedding into other applications. It enables a range of non-breaking API changes that would be fatal in other languages.

The cost is that you have to write tests.

Thirty years ago, when Python was invented, I'm sure this seemed an unnecessary burden. These days, engineering best practices recommend tests for all code, regardless of language.

But rather than being a burden, writing tests for dynamic code is actually the best way to ensure your architecture is well designed. Not because the tests themselves are magic, but because they interact with your source code from a perspective that essentially forces you into good design.

# Well Designed Architectures

To set some context, here are a few of my characteristics of well-designed architectures. (You may disagree with these, in which case I will assume you have poorly designed software.)

* easy to navigate (find a piece of code based on what it does)
* easy to modify (change a piece of code without impacting its dependents)
* easy to identify side effects (tell whether a piece of code has external impact)
* easy to avoid side effects (all external impact can be bypassed)

The following sections will briefly describe what I mean by these characteristics, and suggest how writing tests will help you achieve them.

# Easy to navigate

TODO

* unit tests, lead to...
* separate functions, lead to...
* better naming, lead to...
* better sequencing

# Easy to modify

* separate functions with sequence have implied in/outs
* unit tests for in/outs prove lack of change

# Easy to identify side effects

* tests dislike side effects (ordering, reset, etc.)
* side-effect free logic in transform steps
* side-effect logic in apply steps

# Easy to avoid side effects

* carefully (or don't) test apply steps, or, inject applier functions

# Summary

While easily-measured metrics are a popular way to evaluate code complexity, they can never give you the full story.

Regardless of how much code you have, if it's easy to navigate, easy to modify, and easy to identify and avoid side effects, you've likely got complexity under control.

And the easiest way to control complexity in Python is to write tests. Not because test coverage magically results in better code, but because architecting your code to be testable is the same as architecting for humans.