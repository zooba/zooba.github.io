/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Commands;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.PythonTools.Navigation;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Microsoft.Win32;

namespace Microsoft.PythonTools {
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>    
    [PackageRegistration(UseManagedResourcesOnly = true)]       // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is a package.
    [InstalledProductRegistration("#110", "#112", "1.0",        // This attribute is used to register the informations needed to show the this package in the Help/About dialog of Visual Studio.
        IconResourceID = 400)]
    [ProvideMenuResource(1000, 1)]                              // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideAutoLoad(CommonConstants.UIContextNoSolution)]
    [ProvideAutoLoad(CommonConstants.UIContextSolutionExists)]
    [Description("Python Tools Package")]
    [ProvideAutomationObject("VsPython")]
    [ProvideLanguageEditorOptionPage(typeof(PythonAdvancedEditorOptionsPage), PythonConstants.LanguageName, "", "Advanced", "114")]
    [ProvideOptionPage(typeof(PythonInterpreterOptionsPage), "Python Tools", "Interpreters", 115, 116, true)]
    [ProvideOptionPage(typeof(PythonInteractiveOptionsPage), "Python Tools", "Interactive Windows", 115, 117, true)]
    [ProvideOptionPage(typeof(PythonAdvancedOptionsPage), "Python Tools", "Advanced", 115, 118, true)]
    [Guid(GuidList.guidPythonToolsPkgString)]              // our packages GUID        
    [ProvideLanguageService(typeof(PythonLanguageInfo), PythonConstants.LanguageName, 106, RequestStockColors = true, ShowSmartIndent = true, ShowCompletion = true, DefaultToInsertSpaces = true, HideAdvancedMembersByDefault = false, EnableAdvancedMembersOption = true, ShowDropDownOptions = true)]
    [ProvideLanguageExtension(typeof(PythonLanguageInfo), PythonConstants.FileExtension)]
    [ProvideLanguageExtension(typeof(PythonLanguageInfo), PythonConstants.WindowsFileExtension)]
    [ProvideDebugEngine("Python Debugging", typeof(AD7ProgramProvider), typeof(AD7Engine), AD7Engine.DebugEngineId)]
    [ProvideDebugLanguage("Python", "{DA3C7D59-F9E4-4697-BEE7-3A0703AF6BFF}", AD7Engine.DebugEngineId)]
    [ProvidePythonExecutionModeAttribute(ExecutionMode.StandardModeId, "Standard", "Standard")]
    [ProvidePythonExecutionModeAttribute("{91BB0245-B2A9-47BF-8D76-DD428C6D8974}", "IPython", "visualstudio_ipython_repl.IPythonBackend", false)]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.ArithmeticError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.AssertionError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.AttributeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.BaseException")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.BufferError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.BytesWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.DeprecationWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.EOFError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.EnvironmentError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.Exception")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.FloatingPointError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.FutureWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.GeneratorExit")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.IOError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.ImportError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.ImportWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.IndentationError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.IndexError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.KeyError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.KeyboardInterrupt")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.LookupError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.MemoryError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.NameError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.NotImplementedError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.OSError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.OverflowError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.PendingDeprecationWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.ReferenceError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.RuntimeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.RuntimeWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.StandardError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.StopIteration")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.SyntaxError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.SyntaxWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.SystemError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.SystemExit")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.TabError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.TypeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.UnboundLocalError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.UnicodeDecodeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.UnicodeEncodeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.UnicodeError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.UnicodeTranslateError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.UnicodeWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.UserWarning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.ValueError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.Warning")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.WindowsError")]
    [ProvideDebugException(AD7Engine.DebugEngineId, "Python Exceptions", "exceptions", "exceptions.ZeroDivisionError")]
    public sealed class PythonToolsPackage : CommonPackage {
        private LanguagePreferences _langPrefs;
        public static PythonToolsPackage Instance;
        private ProjectAnalyzer _analyzer;
        private static Dictionary<command, menucommand=""> _commands = new Dictionary<command,menucommand>();
        private PythonAutomation _autoObject = new PythonAutomation();
        private IContentType _contentType;

        /// <summary>
        /// Default constructor of the package.
        /// Inside this method you can place any initialization code that does not require 
        /// any Visual Studio service because at this point the package object is created but 
        /// not sited yet inside Visual Studio environment. The place to do all the other 
        /// initialization is the Initialize method.
        /// </summary>
        public PythonToolsPackage() {
            Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));
            Instance = this;
        }

        internal static void NavigateTo(string filename, Guid docViewGuidType, int line, int col) {
            IVsTextView viewAdapter;
            IVsWindowFrame pWindowFrame;
            OpenDocument(filename, out viewAdapter, out pWindowFrame);
            
            ErrorHandler.ThrowOnFailure(pWindowFrame.Show());

            // Set the cursor at the beginning of the declaration.
            ErrorHandler.ThrowOnFailure(viewAdapter.SetCaretPos(line, col));
            // Make sure that the text is visible.
            viewAdapter.CenterLines(line, 1);
        }

        internal static ITextBuffer GetBufferForDocument(string filename) {
            IVsTextView viewAdapter;
            IVsWindowFrame frame;
            OpenDocument(filename, out viewAdapter, out frame);

            IVsTextLines lines;
            ErrorHandler.ThrowOnFailure(viewAdapter.GetBuffer(out lines));
            
            var model = Instance.GetService(typeof(SComponentModel)) as IComponentModel;
            var adapter = model.GetService<ivseditoradaptersfactoryservice>();

            return adapter.GetDocumentBuffer(lines);            
        }

        private static void OpenDocument(string filename, out IVsTextView viewAdapter, out IVsWindowFrame pWindowFrame) {
            IVsTextManager textMgr = (IVsTextManager)Instance.GetService(typeof(SVsTextManager));

            IVsUIShellOpenDocument uiShellOpenDocument = Instance.GetService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
            IVsUIHierarchy hierarchy;
            uint itemid;


            VsShellUtilities.OpenDocument(
                Instance,
                filename,
                Guid.Empty,
                out hierarchy,
                out itemid,
                out pWindowFrame,
                out viewAdapter);
        }

        protected override object GetAutomationObject(string name) {
            if (name == "VsPython") {
                return _autoObject;
            }

            return base.GetAutomationObject(name);
        }

        public override bool IsRecognizedFile(string filename) {
            return PythonProjectNode.IsPythonFile(filename);
        }

        public override Type GetLibraryManagerType() {
            return typeof(IPythonLibraryManager);
        }

        public string InteractiveOptions {
            get {
                // FIXME
                return "";
            }
        }

        public PythonAdvancedOptionsPage OptionsPage {
            get {
                return (PythonAdvancedOptionsPage)GetDialogPage(typeof(PythonAdvancedOptionsPage));
            }
        }

        internal PythonAdvancedEditorOptionsPage AdvancedEditorOptionsPage {
            get {
                return (PythonAdvancedEditorOptionsPage)GetDialogPage(typeof(PythonAdvancedEditorOptionsPage));
            }
        }

        internal PythonInterpreterOptionsPage InterpreterOptionsPage {
            get {
                return (PythonInterpreterOptionsPage)GetDialogPage(typeof(PythonInterpreterOptionsPage));
            }
        }

        internal PythonInteractiveOptionsPage InteractiveOptionsPage {
            get {
                return (PythonInteractiveOptionsPage)GetDialogPage(typeof(PythonInteractiveOptionsPage));
            }
        }

        /// <summary>
        /// The analyzer which is used for loose files.
        /// </summary>
        internal ProjectAnalyzer DefaultAnalyzer {
            get {
                if (_analyzer == null) {
                    _analyzer = CreateAnalyzer();
                }
                return _analyzer;
            }
        }

        internal void RecreateAnalyzer() {
            _analyzer = CreateAnalyzer();
        }

        private ProjectAnalyzer CreateAnalyzer() {
            var model = GetService(typeof(SComponentModel)) as IComponentModel;

            var defaultFactory = GetDefaultInterpreter(model.GetAllPythonInterpreterFactories());
            EnsureCompletionDb(defaultFactory);
            return new ProjectAnalyzer(defaultFactory.CreateInterpreter(), defaultFactory, model.GetService<ierrorproviderfactory>());
        }

        /// <summary>
        /// Asks the interpreter to auto-generate it's completion database if it doesn't already exist and the user
        /// hasn't disabled this option.
        /// </summary>
        internal static void EnsureCompletionDb(IPythonInterpreterFactory fact) {
            if (PythonToolsPackage.Instance.OptionsPage.AutoAnalyzeStandardLibrary) {
                IInterpreterWithCompletionDatabase interpWithDb = fact as IInterpreterWithCompletionDatabase;
                if (interpWithDb != null) {
                    interpWithDb.AutoGenerateCompletionDatabase();
                }
            }
        }

        private static Guid _noInterpretersFactoryGuid = new Guid("{15CEBB59-1008-4305-97A9-CF5E2CB04711}");
        private static IPythonInterpreterFactory _noInterpretersFactory;

        internal IPythonInterpreterFactory GetDefaultInterpreter(IPythonInterpreterFactory[] factories) {
            IPythonInterpreterFactory lastInterpreter = null, defaultInterpreter = null;
            foreach (var interpreter in factories) {
                lastInterpreter = interpreter;

                if (interpreter.Id == InterpreterOptionsPage.DefaultInterpreter &amp;&amp;
                    interpreter.Configuration.Version == InterpreterOptionsPage.DefaultInterpreterVersion) {
                    defaultInterpreter = interpreter;
                    break;
                }
            }

            if (defaultInterpreter == null &amp;&amp; lastInterpreter != null) {
                // default interpreter not configured, just select the last one and make it the default.
                defaultInterpreter = lastInterpreter;
                InterpreterOptionsPage.DefaultInterpreter = defaultInterpreter.Id;
                InterpreterOptionsPage.DefaultInterpreterVersion = defaultInterpreter.Configuration.Version;
                InterpreterOptionsPage.SaveSettingsToStorage();
            }

            if (defaultInterpreter == null) {
                // no interpreters installed, create a default interpreter for analysis
                if (_noInterpretersFactory == null) {
                    _noInterpretersFactory = ComponentModel.GetService<idefaultinterpreterfactorycreator>().CreateInterpreterFactory(
                        new Dictionary<interpreterfactoryoptions, object="">() {
                            { InterpreterFactoryOptions.Description, "Python 2.7 - No Interpreters Installed" },
                            { InterpreterFactoryOptions.Guid, _noInterpretersFactoryGuid }
                        }
                    );
                }
                defaultInterpreter = _noInterpretersFactory;
            }

            return defaultInterpreter;
        }

        private void UpdateDefaultAnalyzer(object sender, EventArgs args) {
            // no need to update if analyzer isn't created yet.
            if (_analyzer != null) {
                var analyzer = CreateAnalyzer();

                if (_analyzer != null) {
                    analyzer.SwitchAnalyzers(_analyzer);
                }
            }
        }

        internal override LibraryManager CreateLibraryManager(CommonPackage package) {
            return new PythonLibraryManager((PythonToolsPackage)package);
        }

        public IVsSolution Solution {
            get {
                return GetService(typeof(SVsSolution)) as IVsSolution;
            }
        }

        internal static new RegistryKey UserRegistryRoot {
            get {
                if (Instance != null) {
                    return ((CommonPackage)Instance).UserRegistryRoot;
                }


                return Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\VisualStudio\\10.0");
            }
        }

        internal static new RegistryKey ApplicationRegistryRoot {
            get {
                if (Instance != null) {
                    return ((CommonPackage)Instance).ApplicationRegistryRoot;
                }


                return Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\VisualStudio\\10.0");
            }
        }

        /////////////////////////////////////////////////////////////////////////////
        // Overriden Package Implementation

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initilaization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize() {
            Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
            base.Initialize();

            // register our language service so that we can support features like the navigation bar
            var langService = new PythonLanguageInfo(this);
            ((IServiceContainer)this).AddService(langService.GetType(), langService, true);

            var solution = (IVsSolution)Package.GetGlobalService(typeof(SVsSolution));
            //ErrorHandler.ThrowOnFailure(solution.AdviseSolutionEvents(new SolutionAdvisor(), out cookie));

            IVsTextManager textMgr = (IVsTextManager)Instance.GetService(typeof(SVsTextManager));
            var langPrefs = new LANGPREFERENCES[1];
            langPrefs[0].guidLang = typeof(PythonLanguageInfo).GUID;
            ErrorHandler.ThrowOnFailure(textMgr.GetUserPreferences(null, null, langPrefs, null));
            _langPrefs = new LanguagePreferences(langPrefs[0]);

            Guid guid = typeof(IVsTextManagerEvents2).GUID;
            IConnectionPoint connectionPoint;
            ((IConnectionPointContainer)textMgr).FindConnectionPoint(ref guid, out connectionPoint);
            uint cookie;
            connectionPoint.Advise(_langPrefs, out cookie);

            var model = GetService(typeof(SComponentModel)) as IComponentModel;

            // Add our command handlers for menu (commands must exist in the .vsct file)
            RegisterCommands(new Command[] { new ExecuteInReplCommand(), new SendToReplCommand(), new FillParagraphCommand(), new SendToDefiningModuleCommand() });
            RegisterCommands(GetReplCommands());
            
            InterpreterOptionsPage.InterpretersChanged += InterpretersChanged;
            InterpreterOptionsPage.DefaultInterpreterChanged += UpdateDefaultAnalyzer;
        }

        private void InterpretersChanged(object sender, EventArgs e) {
            RefreshReplCommands();
        }

        private void RegisterCommands(IEnumerable<command></command> commands) {
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null != mcs) {
                foreach (var command in commands) {
                    var beforeQueryStatus = command.BeforeQueryStatus;
                    CommandID toolwndCommandID = new CommandID(GuidList.guidPythonToolsCmdSet, command.CommandId);
                    if (beforeQueryStatus == null) {
                        MenuCommand menuToolWin = new MenuCommand(command.DoCommand, toolwndCommandID);                        
                        mcs.AddCommand(menuToolWin);
                        _commands[command] = menuToolWin;
                    } else {
                        OleMenuCommand menuToolWin = new OleMenuCommand(command.DoCommand, toolwndCommandID);                        
                        menuToolWin.BeforeQueryStatus += beforeQueryStatus;
                        mcs.AddCommand(menuToolWin);
                        _commands[command] = menuToolWin;
                    }
                }
            }
        }

        internal void RefreshReplCommands() {
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            List<openreplcommand> replCommands = new List<openreplcommand>();
            foreach (var keyValue in _commands) {
                var command = keyValue.Key;
                OpenReplCommand openRepl = command as OpenReplCommand;
                if (openRepl != null) {
                    replCommands.Add(openRepl);

                    mcs.RemoveCommand(keyValue.Value);
                }
            }

            foreach (var command in replCommands) {
                _commands.Remove(command);
            }

            RegisterCommands(GetReplCommands());
        }

        private List<openreplcommand> GetReplCommands() {
            var factories = ComponentModel.GetAllPythonInterpreterFactories();
            var defaultFactory = GetDefaultInterpreter(factories);
            // sort so default always comes first, and otherwise in sorted order
            Array.Sort(factories, (x, y) =&gt; {
                if (x == y) {
                    return 0;
                } else if (x == defaultFactory) {
                    return -1;
                } else if (y == defaultFactory) {
                    return 1;
                } else {
                    return String.Compare(x.GetInterpreterDisplay(), y.GetInterpreterDisplay());
                }
            });

            var replCommands = new List<openreplcommand>();
            for (int i = 0; i &lt; (PkgCmdIDList.cmdidReplWindowF - PkgCmdIDList.cmdidReplWindow) &amp;&amp; i &lt; factories.Length; i++) {
                var factory = factories[i];

                var cmd = new OpenReplCommand((int)PkgCmdIDList.cmdidReplWindow + i, factory);
                replCommands.Add(cmd);
            }
            return replCommands;
        }

        internal static bool TryGetStartupFileAndDirectory(out string filename, out string dir, out ProjectAnalyzer analyzer) {
            var startupProject = GetStartupProject();
            if (startupProject != null) {
                filename = startupProject.GetStartupFile();
                dir = startupProject.GetWorkingDirectory();
                analyzer = ((PythonProjectNode)startupProject).GetAnalyzer();
            } else {
                var textView = CommonPackage.GetActiveTextView();
                if (textView == null) {
                    filename = null;
                    dir = null;
                    analyzer = null;
                    return false;
                }
                filename = textView.GetFilePath();
                analyzer = textView.GetAnalyzer();
                dir = Path.GetDirectoryName(filename);
            }
            return true;
        }

        internal LanguagePreferences LangPrefs {
            get {
                return _langPrefs;
            }
        }

        public EnvDTE.DTE DTE {
            get {
                return (EnvDTE.DTE)GetService(typeof(EnvDTE.DTE));
            }
        }

        public IContentType ContentType {
            get {
                if (_contentType == null) {
                    _contentType = ComponentModel.GetService<icontenttyperegistryservice>().GetContentType(PythonCoreConstants.ContentType);
                }
                return _contentType;
            }
        }

        internal static Dictionary<command, menucommand=""> Commands {
            get {
                return _commands;
            }
        }

        // This is duplicated throughout different assemblies in PythonTools, so search for it if you update it.
        internal static string GetPythonToolsInstallPath() {
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (File.Exists(Path.Combine(path, "PyDebugAttach.dll"))) {
                return path;
            }

            // running from the GAC in remote attach scenario.  Look to the VS install dir.
            using (var configKey = OpenVisualStudioKey()) {
                var installDir = configKey.GetValue("InstallDir") as string;
                if (installDir != null) {
                    var toolsPath = Path.Combine(installDir, "Extensions\\Microsoft\\Python Tools for Visual Studio\\1.0");
                    if (File.Exists(Path.Combine(toolsPath, "PyDebugAttach.dll"))) {
                        return toolsPath;
                    }
                }
            }

            return null;
        }

        private static Win32.RegistryKey OpenVisualStudioKey() {
            if (Environment.Is64BitOperatingSystem) {
                return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey("Software\\Microsoft\\VisualStudio\\10.0");
            } else {
                return Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\VisualStudio\\10.0");
            }
        }

    }
}
