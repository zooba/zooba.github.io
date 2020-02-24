---
layout: post
title: New New Project From Existing Code
slug: new-new-project-from-existing-code
category: Python Tools
tags:
- python
- PTVS
- project
- Visual Studio
---

[Last time](/blog/new-project-from-existing-code/) I wrote about the New Project from Existing Code wizard in Visual Studio and how we extended it to provide support for Python. This time, I'm going to look at the replacement for this dialog.

As discussed, there are a number of issues with reusing the managed languages wizard (C# and VB) for Python, largely due to the fact that it was never intended to be extensible. It is also difficult or impossible to (reliably) provide an alternate implementation, and in any case the feature is not very easy to discover. Because the usual way to create a new project normally involves skipping the menu and going straight to the list of templates, the aim was to add an item to this dialog:

[![New Project dialog with From Existing Python code selected](/assets/ip2_new_project-300x182.png)](/assets/ip2_new_project.png)

The three steps involved are creating the wizard, providing the necessary interfaces for Visual Studio and creating a template to start the wizard. The code is from changeset `a8d12570c484`, which was added prior to PTVS 1.5RC.

# Designing a Wizard

Since this approach does not rely on reusing existing code, we were free to design the wizard in a way that flows nicely for Python developers and only exposes those options that are relevant, such as the interpreter to use with a project and the main script file. Because not every option needs to be used, and there are obviously ways to change them later, the wizard was set up with two pages. The first page collected the information that is essential to importing a project, this being the source of the files, a filter for file types and any non-standard search paths.

[![New Project from Existing Python Code Wizard page one](/assets/ip2_wizard1-300x216.png)](/assets/ip2_wizard1.png)

To avoid forcing the user to guess what each value means or how it behaves, lighter explanatory text is included directly in the window. (In earlier days these would have been tooltips, but with touch starting to become more prominent, requiring hovering is poor UI design.) The tone is deliberately casual and reassuring - one of the surprises people often find with Visual Studio is that adding an existing file will copy it into the project folder. When importing very large projects, it is far more desirable to leave the files alone and put the project in a nearby location. Because we support a `ProjectHome` element in our `.pyproj` files, we can treat any folder as the root of the project (this will no doubt be the subject of a post in the future).

The second page is entirely optional, and while it cannot be skipped entirely from the first, once users are familiar with the dialog it is very easy to click through without changing the default selections:

[![New Project from Existing Python Code Wizard page two](/assets/ip2_wizard2-300x216.png)](/assets/ip2_wizard2.png)

The two options on this page relate to Visual Studio settings, specifically, which version of Python should be used when running from within VS and which file should be used for F5/Ctrl+F5 execution (as opposed to using the "Start with/without Debugging" option on a specific file). Again, the light grey explanatory text reassures the user that any selection made here is not permanent and provides directions on how to make changes later. The second option - which file to run on F5 - also suggests that not all files will appear in the list. For performance reasons, only `*.py` (and `*.pyw`) files in the root directory are shown, since showing all files would require a recursive directory traversal (which is slow) and produce a much longer list of files (hard to navigate). Since Python does not allow the `import` statement to traverse up from the started script (in typical uses), most projects will have their main file in the root of the project. (That said, there is [a chance](http://pytools.codeplex.com/workitem/891) that this aspect of the dialog will change for the next release, either by including all files, switching to a treeview or simply not being offered as an option.)

When "Finish" is clicked, the rest of the task is quite straightforward: the files in the provided path are scanned for all those matching the filter and the `$variables$` in [FromExistingCode.pyproj](https://github.com/zooba/zooba.github.io/tree/master/assets/ptvscode/FromExistingCode.pyproj) are replaced. This produces a `.pyproj` file that is then loaded normally. (Contrast with the [other approach](/blog/new-project-from-existing-code/) that creates an empty project and adds each file individually. This way is much faster.) Details are in the following section.

# IWizard and replacementsDictionary

Template wizards in Visual Studio are implemented by providing the [IWizard](http://msdn.microsoft.com/en-us/library/microsoft.visualstudio.templatewizard.iwizard.aspx) interface. The methods on this interface are called at various points to allow customisation of the template, but only one method is of interest here: [RunStarted](http://msdn.microsoft.com/en-us/library/microsoft.visualstudio.templatewizard.iwizard.runstarted.aspx). The [how-to](http://msdn.microsoft.com/en-us/library/ms185301.aspx) page covers the process in detail, but the basic idea is that `RunStarted` displays the user interface and updates the set of replacement strings, which are then applied to existing template files.

The only template file used is [FromExistingCode.pyproj](https://github.com/zooba/zooba.github.io/tree/master/assets/ptvscode/FromExistingCode.pyproj), which contains five variables for replacement: `$projecthome$`, `$startupfile$`, `$searchpaths$`, `$interpreter$` and `$content$`. While the first three are simple values, `$interpreter$` will be replaced by the GUID and version (`InterpreterId` and `InterpreterVersion` properties) that represents the interpreter selected on page two of the wizard, and `$content$` is replaced by the list of files and folders. Strictly, this is a slight misuse of the templating system, which is intended for values rather than code/XML generation, but it works and is quite efficient.

When `RunStarted` is called (by VS), a dictionary is provided for the wizard to fill in with these values. This means that a lot of processing takes place within this one function, which is generally not how callbacks like this should behave. However, in this case, it is perfectly appropriate to use modal dialogs and let exceptions propagate - in particular, [WizardBackoutException](http://msdn.microsoft.com/en-us/library/microsoft.visualstudio.templatewizard.wizardbackoutexception.aspx) should be thrown if the user cancels out of the dialog (unlike the [WizardCancelledException](http://msdn.microsoft.com/en-us/library/microsoft.visualstudio.templatewizard.wizardcancelledexception.aspx), backing out returns the user to the template selection dialog).

`RunStarted` is implemented (along with the other methods) in [ImportWizard.cs](https://github.com/zooba/zooba.github.io/tree/master/assets/ptvscode/ImportWizard.cs), with UI and XML generation separated into other methods to allow for easier testing.

```csharp
public void RunStarted(object automationObject, Dictionary<string, string=""> replacementsDictionary, WizardRunKind runKind, object[] customParams) {
    try {
        var provider = new ServiceProvider((Microsoft.VisualStudio.OLE.Interop.IServiceProvider)automationObject);
        var settings = ImportWizardDialog.ShowImportDialog(provider);

        if (settings == null) {
            throw new WizardBackoutException();
        }

        SetReplacements(settings, replacementsDictionary);
    } catch (WizardBackoutException) {
        try {
            Directory.Delete(replacementsDictionary["$destinationdirectory$"]);
        } catch {
            // If it fails (doesn't exist/contains files/read-only), let the directory stay.
        }
        throw;
    } catch (Exception ex) {
        MessageBox.Show(string.Format("Error occurred running wizard:\n\n{0}", ex));
        throw new WizardCancelledException("Internal error", ex);
    }
}
```

One point worth expanding on is the `Directory.Delete` call in the cancellation handler. Because this is a new project wizard, VS creates the destination directory based on user input before the wizard starts. However, if the wizard is cancelled from within `RunStarted`, as opposed to failing before `RunStarted` can be called, the directory is not removed. To prevent the user from seeing empty directories appear in their Projects folder, we try and remove it. (That said, we don't try very hard - if the directory has ended up with files in it already then it will not be removed.)

# The .vstemplate File

The final piece of this feature is adding the entry to the New Project dialog and starting the wizard when selected by the user. Templates in Visual Studio are typically added by including `.vstemplate` files in a registered folder (optionally creating a registering a folder specific to an extension). These files are XML and specify the template properties and the list of files to copy to the destination directory. For templates that include wizards, an optional `WizardExtension` element can be added, as seen in [FromExistingCode.vstemplate](https://github.com/zooba/zooba.github.io/tree/master/assets/ptvscode/FromExistingCode.vstemplate):

```xml
<wizardextension>
  <assembly>Microsoft.PythonTools.ImportWizard, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a</assembly>
  <fullclassname>Microsoft.PythonTools.ImportWizard.Wizard</fullclassname>
</wizardextension>
```

Importantly, the assembly name must be a fully qualified name, which is why we used a fixed version number for this file (the rest of our assemblies use an automatically generated build number, which is fine for compiled code but not so simple to include in a data file that is embedded in a `.zip` file). `Wizard` is the name of the class in [ImportWizard.cs](https://github.com/zooba/zooba.github.io/tree/master/assets/ptvscode/ImportWizard.cs) that implements `IWizard`, and so VS is able to instantiate `Wizard` and invoke `RunStarted` as part of creating the new project. The entire template is simply added to our existing templates directory, and VS discovers the `.vstemplate` file and includes it in the list.

One of the concerns with the previous version of this wizard was discoverability: it was not easy to find the feature. To completely solve this problem, we set the `SortOrder` value of the template to 10, which is very low and all but guarantees that it will always appear first in the list. So now the first option that will appear to both new and existing users of PTVS is a simple way to use their projects without having to add each file individually.

# Summary

In this post we looked at the new New Project from Existing Code wizard that replaced the one from [last time](/blog/new-project-from-existing-code/). Not using the existing implementation allowed us to design the wizard specifically for Python code and implement it efficiently. We also made the feature more discoverable by placing it first in the list of templates, which is how new projects are usually created. This feature was first released in [Python Tools for Visual Studio](http://pytools.codeplex.com/) 1.5RC.