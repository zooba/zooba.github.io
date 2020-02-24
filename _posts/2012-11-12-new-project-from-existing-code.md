---
layout: post
title: New Project From Existing Code
slug: new-project-from-existing-code
category: Python Tools
tags:
- python
- PTVS
- project
- Visual Studio
---

In this post, we'll be looking at how version one of a particular feature was implemented. The implementation was not very good (I can say that, since I contributed it), but it filled a necessary gap until it could be done better. [Python Tools for Visual Studio](http://pytools.codeplex.com/) 1.1 had this implementation, while PTVS 1.5 uses a completely different approach (that might be the subject of a future blog, but not this one).

When creating a new project in Visual Studio, you typically begin from a list of templates that are specific to your language:

[![Visual Basic project templates](/assets/ip_vbtemplates-300x182.png)](/assets/ip_vbtemplates.png)

If you already have a set of existing code, the obvious approach is to select "Empty Project" and add the files to that project. This situation is common when working on cross-platform projects, which often do not include Visual Studio projects, or when you want to migrate from earlier versions without introducing the compatibility 'hacks' provided by the upgrade wizard. An alternative to manually importing the files into an empty project is to use the "Project from Existing Code" wizard, which is available under the "New Project" submenu:

[![Project from Existing Code menu item](/assets/ip_menuitem-300x113.png)](/assets/ip_menuitem.png)

By default, this wizard only supports C#, VB and C++, but since very few Python projects ever come with project files, importing existing code is a very common task. This feature added basic support for Python projects to the existing wizard. The code is from changeset `156ceb1b1949`, which was part of the original pull request that was merged into PTVS 1.0.

# Adding Python to the list

The default wizard only provides support for C#, VB and C++.

[![Import Wizard window](/assets/ip_wizard-300x266.png)](/assets/ip_wizard.png)

Luckily, since Visual Studio is endlessly extensible, these project types are not hardcoded, but are found in the registry. For example, the C# entries are found in the `Projects\{FAE04EC0-301F-11d3-BF4B-00C04F79EFBC}\ImportTemplates` subkey of the Visual Studio key:

[![C# ImportTemplates registry entry](/assets/ip_csregistry-300x117.png)](/assets/ip_csregistry.png)

The meaning of each value is relatively straightforward, but to summarise:

WizardPageObjectAssembly
: Full name of the assembly containing the wizard implementation.
WizardPageObjectClass
: Full name of the call implementing the wizard.
ProjectType
: The name (or a string resource within the project type's package) to display in the dropdown box.
ImportProjectsDir
: Directory containing the following three files.
ClassLibProjectFile
: Filename of the project template for a class library project.
ConsoleAppProjectFile
: Filename of the project template for a console application project.
WindowsAppProjectFile
: Filename of the project template for a windowed application project.

Those last four items are specific to the `Microsoft.VsWizards.ImportProjectFolderWizard.Managed.PageManager` class, which is used for C# and VB projects only. The C++ wizard uses a different implementation that is very specific to C++, but the one for C# and VB is far more general and can be easily adapted to Python without requiring any new code (except to set up the registry entries). We could also provide a completely new class, but in the interest of minimising new code, that wasn't done this time around.

To register the existing importer for Python projects, a [ProvideImportTemplatesAttribute](https://github.com/zooba/zooba.github.io/tree/master/assets/ptvscode/ProvideImportTemplatesAttribute.cs) was created, since package attributes are how registry keys are added for PTVS (rather than specifying them in the installer). It was applied to the project package as:

```csharp
[ProvideImportTemplates("#111",
                        PythonConstants.ProjectFactoryGuid,
                        "$PackageFolder$\\Templates\\Projects\\ImportProject",
                        "ImportWinApp.pyproj",
                        "ImportConApp.pyproj",
                        "ImportConApp.pyproj")]
```

The parameters map very closely to the values shown above for C#. PTVS has no concept of a library project, so we use a standard console project: the wizard implementation is... fragile... and while you could hope that leaving the class library template out would disable the option, in reality is simply causes an exception. Each template was created simply by using the existing template with the initial `Program.py` removed.

Adding this attribute is sufficient to make "Python Project" (the value of the "#111") appear in the wizard, but not to actually import the project. This second step required a little more work inside the implementation of the PTVS project system.

# The AddFromDirectory method

Among all the classes required to implement the project system, it is very rare to see dead code. Methods are only implemented if they are called, and as it turned out, a method required for importing existing code had never been written. This was the [AddFromDirectory](http://msdn.microsoft.com/en-US/library/envdte.projectitems.addfromdirectory(v=vs.110).aspx) method in [OAProjectItems.cs](https://github.com/zooba/zooba.github.io/tree/master/assets/ptvscode/OAProjectItems.cs).

In brief (not that I can be much more brief than that MSDN page), `AddFromDirectory` recursively adds a folder and all the files and folders it contains into the project. The directory provided is returned as a new project folder (as if the user had clicked on "New Folder" themselves), which means that this function cannot actually be used to import the entire project. As a workaround, the wizard implementation adds any top-level files directly, then calls `AddFromDirectory` for each top-level folder.

Implementing `AddFromDirectory` is very simple, since the existing `AddDirectory` and `AddFromFile` methods can be used. The original implementation filtered files to only include those that could be `import`ed from Python (in effect, `*.py` and `*.pyw` files in directories containing an `__init__.py` file), though this was later relaxed based on user feedback.

# Problems

While this approach is very simple, it has a number of drawbacks. Those that have already been mentioned include an irrelevant option for a "Class Library" and the inability to filter top-level files and directories. These could be easily resolved by substituting another wizard class, but this is actually the point where the extensibility breaks down.

Import wizards are .NET classes that implement `Microsoft.VsWizards.ImportProjectFolderWizard.IPageManager` from the `Microsoft.VisualStudio.ImportProjectFolderWizard.dll` assembly that ships with Visual Studio. Unfortunately, this assembly is not stored anywhere that can be safely referenced without assuming the full VS installation path, cannot be redistributed (and probably cannot be loaded multiple times from different locations anyway), and is not guaranteed to be loaded before another assembly containing wizards. Basically, while it is possible to _create_ a new wizard, it is not possible to safely _load_ that wizard, making this idea pretty useless.

The final problem that was encountered was discoverability. Despite documentation, a video and announcements, it is not easy to find this feature, and even the other languages suffer from this. It would seem that people generally use other ways to open the New Project window (since there are at least four) and so never even see the "From Existing Code" option. In the 1.5 release of PTVS we've tried to solve this by providing the wizard as an item in the normal templates list:

[![Python project templates with PTVS 1.5](/assets/ip_pytemplates-300x213.png)](/assets/ip_pytemplates.png)

# Summary

It is possible to extend the "New Project from Existing Code" wizard by providing some template files and registry entries. The project system that the wizard is for has to implement `AddFromDirectory` on project items, since this is the function that performs most of the work. Unfortunately, extending by providing a new wizard implementation is not possible because of how the dependent assemblies are distributed and loaded. This feature was originally in [Python Tools for Visual Studio](http://pytools.codeplex.com/) version 1.0, and was replaced in version 1.5 with a more appropriate and discoverable wizard.
