---
layout: post
title: 'Python at Microsoft: flying under the radar'
slug: python-at-microsoft
category: Microsoft
tags:
- python
- Microsoft
- PyCon
- Visual Studio
---

Python is an important piece of Microsoft's future in the cloud, being one of the essential languages for services and teams to support, as well as the most popular choice for the rapidly growing field of data science and analytics both inside and outside of the company. But Python hasn't always had such a prestigious position around Microsoft.

In 2010, our few [Pythonistas](https://en.wiktionary.org/wiki/Pythonista) were flying under the radar, in case somebody noticed that they could reassign a few developers to their own project. The team was small, leftover from a previous job, but was chipping away at a company culture that suffered from ["not invented here"](https://en.wikipedia.org/wiki/Not_invented_here) syndrome: **Python was a language that belonged to other people, and so Microsoft was not interested.**

Over the last eight years, **the change has been dramatic**. Many Microsoft products now include Python support, and some of the newest _only_ support Python. Some of our critical tools are written in Python, and we are actively investing in the language and community.

This is my story. From when I first started at the company in 2011 through to today, I've had the privilege to work on some significant Python-related projects, and work with many of the teams building others. These days, I get to contribute throughout Microsoft to build up our Python muscles and collaborate with the community to make Python better for everyone.

# Python in Visual Studio

Before starting at Microsoft, I was still in grad school in Australia. Already a Visual Studio fan and a Python developer, I was excited to see [the earliest releases of Python Tools for Visual Studio](https://web.archive.org/web/20110312095250/http:/pytools.codeplex.com/) (PTVS) in 2010. IronPython, the version of Python that runs on .NET, had been handed off to the community, and a small team with [Dino Viehland](https://twitter.com/dinoviehland) and [Shahrokh Mortazavi](https://www.linkedin.com/in/sean-mortazavi-5a865642/) was put together to keep building Python support into Microsoft products. After months of negotiation with the legal team, PTVS was published on CodePlex (Microsoft's former open-source hosting service) under the Apache 2.0 license and allowed to accept external contributions.

As a Python developer, I happily took the earliest PTVS builds and reported everything that didn't work well, including fixing some issues. I contributed for a month or so, and then the team's manager asked if I'd be interested to come to Microsoft as a summer intern. One year later I was a full-time member of the Redmond-based team.

Since our team had the most Python knowledge we were the natural home for supporting Python on Microsoft Azure. The earliest versions of the [Azure SDK for Python](https://github.com/Azure/azure-sdk-for-python/) were developed within our team, now five people, and as the importance of Python increased we were able to move that work to a dedicated team. Through his contributions to this SDK, we discovered and quickly hired [Laurent Mazuel](https://twitter.com/lmazuel), who has since been central to the success of the Azure management SDK - coordinating over 100 Azure services to produce one coherent library is a challenge!

Over the following years we watched our projects grow in users and usefulness. Each release would generate buzz in places we rarely promoted our products like Twitter, Reddit and Hacker News, with many people not believing that Microsoft was actually working on anything to do with Python, including our own colleagues. I worked in the office next to one developer for half a year before he understood that we actually had a Python team.

> We often felt like a small startup within a very large company

Our engineering team would attend various Python conferences each year, showing and talking about our tools. We were not yet big enough within Microsoft to be assigned help from the marketing team, so we had engineers handle the planning, staffing, decorating, and running the booth. At times we'd be dashing out of an event venue to local printing places to get better signs printed at the last minute, or the grocery store to get chocolates because we didn't have any real swag. We often felt like a small startup within a very large company!

![Microsoft booth at PyCon US 2014](/assets/pyconus-2014.jpg)

<small>Microsoft booth at PyCon US 2014</small>

For a long time, people inside and outside of the company assumed that PTVS was a community project, without realizing that we were the real Microsoft. But there were some signs of progress. One of the first was when we found the marketing person responsible for [visualstudio.com](https://visualstudio.com) and had him insert "Python" into the main page's list of languages. Unsurprisingly, this caught some people's attention, but in a good way. We started looking "official".

The next turning point came when we were added to the Visual Studio 2015 installer. This release included a revamped "Customize" page that simplified the core options and added a number of "external" components. For the first time, users could have Python support without having to go and download another installer.

![Visual Studio 2015 installer](/assets/vs2015.png)

<small>The Visual Studio 2015 installer featuring Python Tools for Visual Studio</small>

> PTVS moved to its current home on GitHub, the Python SDK for Azure was more popular than expected

As our user numbers skyrocketed, more people started paying attention. We weren't flying under the radar anymore! PTVS moved to its current home on [GitHub](https://github.com/Microsoft/PTVS), the Python SDK for Azure was more popular than expected, and the pieces were falling into place to start real culture change.

# Contributing to Python

Meanwhile, at PyCon US in 2015 I volunteered to help support Python on Windows, an offer which was gladly accepted and, after multiple interviews with the legal teams, I soon became a Microsoft-supported [CPython core developer](https://devguide.python.org/coredev/).

For the Python 3.5 release, I ported Python from using the Microsoft Visual C++ 2010 compiler and runtime to the latest version, including having changes made to our C Runtime specifically for CPython (such as the [_set_thread_local_invalid_parameter_handler() function](https://docs.microsoft.com/cpp/c-runtime-library/reference/set-invalid-parameter-handler-set-thread-local-invalid-parameter-handler)). I also rewrote the installer, fixing per-user installations and changing to properly secured install directories. Finally, I took on the responsibility for building all the Windows versions of Python available from python.org.

![The old Python installer alongside the new one for Python 3.5](/assets/python-installer-sxs.png)

<small>The old Python installer alongside the new one for Python 3.5</small>

Today at Microsoft we have five core CPython committers, and all of us are allowed time to contribute to the project. We've contributed improvements to compatibility, registration, [fancy new icons](https://bugs.python.org/issue27756) ([example.png](https://bugs.python.org/file44129/ProposedIcons.png)), [JIT execution hooks](https://www.python.org/dev/peps/pep-0523/), and have more in progress. Being employed by a large corporation with its own projects helps us see problems and scenarios that we may not if we were purely volunteers. Joined with paid development time, we are able to have a satisfyingly positive impact on the Python community.

# Watching a culture change

> You can tell when a corporation's culture is starting to change when people approach you to ask about Python, rather than you having to approach them.

You can tell when a corporation's culture is starting to change when people approach you to ask about Python, rather than you having to approach them. One project we considered a major victory was the [cross-platform Azure CLI](https://docs.microsoft.com/cli/azure/). Originally written in Node.js, the team behind it was finding it more and more difficult to maintain such a large command-line tool. While investigating alternative approaches, they came to us asking about Python support.

The discussion went roughly like this:

> CLI team: "We don't really know Python, but it seems like Python would be a good choice for an extensible command-line tool."
> Python team: "Yes, it would be. 😊"
> CLI: "Could you perhaps help us out by building the basic structure? Just a few commands, and show us how to preserve login state and do configuration files? Maybe localization?"
> Python: "Of course, happy to."
> CLI: "How many weeks will it take to build something we can try out?"
> Python: "It'll be ready by Wednesday."

Surprised, and probably more skeptical than they let on, the team accepted our offer and I put together [a sample](https://github.com/Azure/azure-cli/tree/549f1be8ee881fb309caf3559d9232d3191af81d). The Azure CLI team took this and have turned it into possibly the largest Python command line application in the world. [Knack](https://github.com/Microsoft/knack) is now its own Python framework for high-performance large-scale command line applications, handling thousands of commands and options while supporting argument completion, prompting, extensibility, configuration files, and more.

> We organized an internal "Python Day" … All told, over 1000 people attended or watched the event - that was about 1% of the entire company.

![Python Day at Microsoft poster](/assets/pythonday.png)

<small>Python Day at Microsoft poster</small>

While we always knew that Python was more popular around Microsoft than you could tell, one event really blew us away. Late 2014, we organized an internal "Python Day", invited some famous people, booked a room for 100 people, and put posters around the offices. The response was amazing! When the day came we had upgraded to one of the largest rooms on our main campus in Redmond and set up a livestream for our other world-wide offices. All told, over 1000 people attended or watched the event - that was about 1% of the entire company (including all of engineering, finance, HR, and legal - we had people from all roles come).

![The Visual Studio 2017 installer featuring a Python workload](/assets/vs2017.png)

<small>The Visual Studio 2017 installer featuring a Python workload</small>

Around the same time, [Visual Studio 2017](https://visualstudio.microsoft.com/vs/features/python/) was being designed with a completely new installer. From early in the process, we were included - not just as a link to an extension, but as a true built-in feature. But we still kept our work public on GitHub, and helped work out the model also used by a number of other Visual Studio components. PTVS was one of the earliest open-source Visual Studio features but now there are many, including [Roslyn](https://github.com/dotnet/roslyn) (the C# and Visual Basic compiler), [MSBuild](https://github.com/Microsoft/MSBuild) and [Visual F#](https://github.com/dotnet/fsharp).

We also adopted the community-built Python extension for Visual Studio Code, hiring its developer [Don Jayamanne](https://twitter.com/DonJayamanne) and giving him full time to work on it along with a team of other developers. Since it started as open source, of course it had to stay that way, and [our official GitHub repository](https://github.com/Microsoft/vscode-python) clearly shows that we forked from the original.

At PyCon US 2019 we will be [keystone sponsor](https://us.pycon.org/2019/sponsors/) for the third time, so expect to see us there. Unlike five years ago, we'll have a properly designed booth and a huge range of demos, and we'll be talking about all the ways we rely on Python, contribute to Python, and are working to enable and support everyone who uses Python.

# No longer under the radar

In 2011, Python at Microsoft was literally trying to fly under the radar. In 2018, we are out and proud about Python, supporting it in our developer tools such as [Visual Studio](https://aka.ms/ptvs) and [Visual Studio Code](https://code.visualstudio.com/docs/python/), hosting it in [Azure Notebooks](https://notebooks.azure.com/), and using it to build end-user experiences like the [Azure CLI](https://aka.ms/azure-cli). We employ five core CPython developers and many other contributors, are strong supporters of open-source data science through [NumFOCUS](https://numfocus.org/) and [PyData](https://pydata.org/), and regularly sponsor, host, and attend Python events around the world.

While I can't personally claim credit for all of the progress we've seen over the last eight years (and more!), it has been a privilege to have been around for it and to have helped lead Microsoft towards being a better member of the open source community.

## Resources

Latest resources about Python at Microsoft are always at [aka.ms/Python](https://aka.ms/python)

Watch our Python video content on [Channel 9](https://channel9.msdn.com/Search?term=python) or [Steve's Python talks](/speaking).

Browse our open-source Python projects in the [Microsoft](https://github.com/Microsoft?utf8=%E2%9C%93&q=&type=&language=python) and [Azure](https://github.com/Azure?utf8=%E2%9C%93&q=&type=&language=python) organizations on GitHub.
