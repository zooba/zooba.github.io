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
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Microsoft.PythonTools.Debugger.DebugEngine {
    // AD7Engine is the primary entrypoint object for the debugging engine. 
    //
    // It implements:
    //
    // IDebugEngine2: This interface represents a debug engine (DE). It is used to manage various aspects of a debugging session, 
    // from creating breakpoints to setting and clearing exceptions.
    //
    // IDebugEngineLaunch2: Used by a debug engine (DE) to launch and terminate programs.
    //
    // IDebugProgram3: This interface represents a program that is running in a process. Since this engine only debugs one process at a time and each 
    // process only contains one program, it is implemented on the engine.

    [ComVisible(true)]
    [Guid("8355452D-6D2F-41b0-89B8-BB2AA2529E94")]
    public sealed class AD7Engine : IDebugEngine2, IDebugEngineLaunch2, IDebugProgram3, IDebugSymbolSettings100 {
        // used to send events to the debugger. Some examples of these events are thread create, exception thrown, module load.
        private IDebugEventCallback2 _events;

        // The core of the engine is implemented by PythonProcess - we wrap and expose that to VS.
        private PythonProcess _process;
        
        // mapping between PythonProcess threads and AD7Threads
        private Dictionary<pythonthread, ad7thread=""> _threads = new Dictionary<pythonthread, ad7thread="">();
        private Dictionary<pythonmodule, ad7module=""> _modules = new Dictionary<pythonmodule, ad7module="">();
        private Dictionary<string, int=""> _breakOnException = new Dictionary<string, int="">();
        private int _defaultBreakOnExceptionMode;
        private AutoResetEvent _loadComplete = new AutoResetEvent(false);
        private bool _programCreated;
        private object _syncLock = new object();
        private AD7Thread _processLoadedThread, _startThread;
        private AD7Module _startModule;
        private bool _attached;
        private BreakpointManager _breakpointManager;
        private Guid _ad7ProgramId;             // A unique identifier for the program being debugged.
        public const string DebugEngineId = "{EC1375B7-E2CE-43E8-BF75-DC638DE1F1F9}";
        public static Guid DebugEngineGuid = new Guid(DebugEngineId);
        /// <summary>
        /// Specifies the version of the language which is being debugged.  One of
        /// V24, V25, V26, V27, V30, V31 or V32.
        /// </summary>
        public const string VersionSetting = "VERSION";

        /// <summary>
        /// Specifies whether the process should prompt for input before exiting on an abnormal exit.
        /// </summary>
        public const string WaitOnAbnormalExitSetting = "WAIT_ON_ABNORMAL_EXIT";

        /// <summary>
        /// Specifies whether the process should prompt for input before exiting on a normal exit.
        /// </summary>
        public const string WaitOnNormalExitSetting = "WAIT_ON_NORMAL_EXIT";

        /// <summary>
        /// Specifies if the output should be redirected to the visual studio output window.
        /// </summary>
        public const string RedirectOutputSetting = "REDIRECT_OUTPUT";

        /// <summary>
        /// Specifies options which should be passed to the Python interpreter before the script.  If
        /// the interpreter options should include a semicolon then it should be escaped as a double
        /// semi-colon.
        /// </summary>
        public const string InterpreterOptions = "INTERPRETER_OPTIONS";

        /// <summary>
        /// Specifies a directory mapping in the form of:
        /// 
        /// OldDir|NewDir
        /// 
        /// for mapping between the files on the local machine and the files deployed on the
        /// running machine.
        /// </summary>
        public const string DirMappingSetting = "DIR_MAPPING";

        public AD7Engine() {            
            _breakpointManager = new BreakpointManager(this);
            _defaultBreakOnExceptionMode = (int)enum_EXCEPTION_STATE.EXCEPTION_STOP_USER_UNCAUGHT;
            Debug.WriteLine("Python Engine Created " + GetHashCode());
        }

        ~AD7Engine() {
            Debug.WriteLine("Python Engine Finalized " + GetHashCode());
            if (!_attached &amp;&amp; _process != null) {
                // detach the process exited event, we don't need to send the exited event
                // which could happen when we terminate the process and check if it's still
                // running.
                _process.ProcessExited -= OnProcessExited;

                // we launched the process, go ahead and kill it now that
                // VS has released us
                _process.Terminate();
            }
        }

        internal PythonProcess Process {
            get {
                return _process;
            }
        }

        internal BreakpointManager BreakpointManager {
            get {
                return _breakpointManager;
            }
        }

        #region IDebugEngine2 Members

        // Attach the debug engine to a program. 
        int IDebugEngine2.Attach(IDebugProgram2[] rgpPrograms, IDebugProgramNode2[] rgpProgramNodes, uint celtPrograms, IDebugEventCallback2 ad7Callback, enum_ATTACH_REASON dwReason) {
            Debug.WriteLine("PythonEngine Attach Begin " + GetHashCode());

            AssertMainThread();
            Debug.Assert(_ad7ProgramId == Guid.Empty);

            if (celtPrograms != 1) {
                Debug.Fail("Python debugging only supports one program in a process");
                throw new ArgumentException();
            }

            int processId = EngineUtils.GetProcessId(rgpPrograms[0]);
            if (processId == 0) {
                // engine only supports system processes
                Debug.WriteLine("PythonEngine failed to get process id during attach");
                return VSConstants.E_NOTIMPL;
            }

            EngineUtils.RequireOk(rgpPrograms[0].GetProgramId(out _ad7ProgramId));

            // Attach can either be called to attach to a new process, or to complete an attach
            // to a launched process
            if (_process == null) {                
                // TODO: Where do we get the language version from?
                _events = ad7Callback;
                
                var attachRes = PythonProcess.TryAttach(processId, out _process);
                if (attachRes != ConnErrorMessages.None) {
                    string msg;
                    switch (attachRes) {
                        case ConnErrorMessages.CannotInjectThread: msg = "Cannot create thread in debuggee process"; break;
                        case ConnErrorMessages.CannotOpenProcess: msg = "Cannot open process for debugging"; break;
                        case ConnErrorMessages.InterpreterNotInitialized: msg = "Python interpreter has not been initialized in this process"; break;
                        case ConnErrorMessages.LoadDebuggerBadDebugger: msg = "Failed to load debugging script (incorrect version of script?)"; break;
                        case ConnErrorMessages.LoadDebuggerFailed: msg = "Failed to compile debugging script"; break;
                        case ConnErrorMessages.OutOfMemory: msg = "Out of memory"; break;
                        case ConnErrorMessages.PythonNotFound: msg = "Python interpreter not found"; break;
                        case ConnErrorMessages.TimeOut: msg = "Timeout while attaching"; break;
                        case ConnErrorMessages.UnknownVersion: msg = "Unknown Python version loaded in process"; break;
                        case ConnErrorMessages.SysNotFound: msg = "sys module not found"; break;
                        case ConnErrorMessages.SysSetTraceNotFound: msg = "settrace not found in sys module"; break;
                        case ConnErrorMessages.SysGetTraceNotFound: msg = "gettrace not found in sys module"; break;
                        case ConnErrorMessages.PyDebugAttachNotFound: msg = "Cannot find PyDebugAttach.dll at " + attachRes; break;
                        default: msg = "Unknown error"; break;
                    }

                    MessageBox.Show("Failed to attach debugger: " + msg);
                    return VSConstants.E_FAIL;
                }
                    
                AttachEvents(_process);
                _attached = true;
            } else {
                if (processId != _process.Id) {
                    Debug.Fail("Asked to attach to a process while we are debugging");
                    return VSConstants.E_FAIL;
                }
                _attached = false;
            }

            AD7EngineCreateEvent.Send(this);

            lock (_syncLock) {
                _programCreated = true;
                

                if (_processLoadedThread != null) {
                    SendLoadComplete(_processLoadedThread);
                }
            }

            Debug.WriteLine("PythonEngine Attach returning S_OK");
            return VSConstants.S_OK;
        }

        private void SendLoadComplete(AD7Thread thread) {
            Debug.WriteLine("Sending load complete" + GetHashCode());
            AD7ProgramCreateEvent.Send(this);

            Send(new AD7LoadCompleteEvent(), AD7LoadCompleteEvent.IID, thread);

            if (_startModule != null) {
                SendModuleLoaded(_startModule);
                _startModule = null;
            }
            if (_startThread != null) {
                SendThreadStart(_startThread);
                _startThread = null;
            }
            _processLoadedThread = null;
            _loadComplete.Set();
        }

        private void SendThreadStart(AD7Thread ad7Thread) {
            Send(new AD7ThreadCreateEvent(), AD7ThreadCreateEvent.IID, ad7Thread);
        }

        private void SendModuleLoaded(AD7Module ad7Module) {
            AD7ModuleLoadEvent eventObject = new AD7ModuleLoadEvent(ad7Module, true /* this is a module load */);

            // TODO: Bind breakpoints when the module loads

            Send(eventObject, AD7ModuleLoadEvent.IID, null);
        }

        // Requests that all programs being debugged by this DE stop execution the next time one of their threads attempts to run.
        // This is normally called in response to the user clicking on the pause button in the debugger.
        // When the break is complete, an AsyncBreakComplete event will be sent back to the debugger.
        int IDebugEngine2.CauseBreak() {
            AssertMainThread();

            return ((IDebugProgram2)this).CauseBreak();
        }

        [Conditional("DEBUG")]
        private static void AssertMainThread() {
            //Debug.Assert(Worker.MainThreadId == Worker.CurrentThreadId);
        }

        // Called by the SDM to indicate that a synchronous debug event, previously sent by the DE to the SDM,
        // was received and processed. The only event we send in this fashion is Program Destroy.
        // It responds to that event by shutting down the engine.
        int IDebugEngine2.ContinueFromSynchronousEvent(IDebugEvent2 eventObject) {
            AssertMainThread();

            if (eventObject is AD7ProgramDestroyEvent) {
                var debuggedProcess = _process;

                _events = null;
                _process = null;
                _ad7ProgramId = Guid.Empty;
                _threads.Clear();
                _modules.Clear();

                debuggedProcess.Close();
            } else {
                Debug.Fail("Unknown syncronious event");
            }

            return VSConstants.S_OK;
        }

        // Creates a pending breakpoint in the engine. A pending breakpoint is contains all the information needed to bind a breakpoint to 
        // a location in the debuggee.
        int IDebugEngine2.CreatePendingBreakpoint(IDebugBreakpointRequest2 pBPRequest, out IDebugPendingBreakpoint2 ppPendingBP) {
            Debug.WriteLine("Creating pending break point");
            Debug.Assert(_breakpointManager != null);
            ppPendingBP = null;

            _breakpointManager.CreatePendingBreakpoint(pBPRequest, out ppPendingBP);
            return VSConstants.S_OK;
        }

        // Informs a DE that the program specified has been atypically terminated and that the DE should 
        // clean up all references to the program and send a program destroy event.
        int IDebugEngine2.DestroyProgram(IDebugProgram2 pProgram) {
            Debug.WriteLine("PythonEngine DestroyProgram");
            // Tell the SDM that the engine knows that the program is exiting, and that the
            // engine will send a program destroy. We do this because the Win32 debug api will always
            // tell us that the process exited, and otherwise we have a race condition.

            return (DebuggerConstants.E_PROGRAM_DESTROY_PENDING);
        }

        // Gets the GUID of the DE.
        int IDebugEngine2.GetEngineId(out Guid guidEngine) {
            guidEngine = new Guid(DebugEngineId);
            return VSConstants.S_OK;
        }

        int IDebugEngine2.RemoveAllSetExceptions(ref Guid guidType) {
            _breakOnException.Clear();
            _defaultBreakOnExceptionMode = (int)enum_EXCEPTION_STATE.EXCEPTION_STOP_USER_UNCAUGHT;

            _process.SetExceptionInfo(_defaultBreakOnExceptionMode, _breakOnException);
            return VSConstants.S_OK;
        }

        int IDebugEngine2.RemoveSetException(EXCEPTION_INFO[] pException) {
            bool sendUpdate = false;
            for (int i = 0; i &lt; pException.Length; i++) {
                if (pException[i].guidType == DebugEngineGuid) {
                    sendUpdate = true;
                    if (pException[i].bstrExceptionName == "Python Exceptions") {
                        _defaultBreakOnExceptionMode = (int)enum_EXCEPTION_STATE.EXCEPTION_STOP_USER_UNCAUGHT;
                    } else {
                        _breakOnException.Remove(pException[i].bstrExceptionName);
                    }
                }
            }

            if (sendUpdate) {
                _process.SetExceptionInfo(_defaultBreakOnExceptionMode, _breakOnException);
            }
            return VSConstants.S_OK;
        }

        int IDebugEngine2.SetException(EXCEPTION_INFO[] pException) {
            bool sendUpdate = false;
            for (int i = 0; i &lt; pException.Length; i++) {
                if (pException[i].guidType == DebugEngineGuid) {
                    sendUpdate = true;
                    if (pException[i].bstrExceptionName == "Python Exceptions") {
                        _defaultBreakOnExceptionMode =
                            (int)(pException[i].dwState &amp; (enum_EXCEPTION_STATE.EXCEPTION_STOP_FIRST_CHANCE | enum_EXCEPTION_STATE.EXCEPTION_STOP_USER_UNCAUGHT));
                    } else {
                        _breakOnException[pException[i].bstrExceptionName] = 
                            (int)(pException[i].dwState &amp; (enum_EXCEPTION_STATE.EXCEPTION_STOP_FIRST_CHANCE | enum_EXCEPTION_STATE.EXCEPTION_STOP_USER_UNCAUGHT));
                    }
                }
            }

            if (sendUpdate) {
                _process.SetExceptionInfo(_defaultBreakOnExceptionMode, _breakOnException);
            }
            return VSConstants.S_OK;
        }

        // Sets the locale of the DE.
        // This method is called by the session debug manager (SDM) to propagate the locale settings of the IDE so that
        // strings returned by the DE are properly localized. The engine is not localized so this is not implemented.
        int IDebugEngine2.SetLocale(ushort wLangID) {
            return VSConstants.S_OK;
        }

        // A metric is a registry value used to change a debug engine's behavior or to advertise supported functionality. 
        // This method can forward the call to the appropriate form of the Debugging SDK Helpers function, SetMetric.
        int IDebugEngine2.SetMetric(string pszMetric, object varValue) {
            return VSConstants.S_OK;
        }

        // Sets the registry root currently in use by the DE. Different installations of Visual Studio can change where their registry information is stored
        // This allows the debugger to tell the engine where that location is.
        int IDebugEngine2.SetRegistryRoot(string pszRegistryRoot) {
            return VSConstants.S_OK;
        }

        #endregion

        #region IDebugEngineLaunch2 Members

        // Determines if a process can be terminated.
        int IDebugEngineLaunch2.CanTerminateProcess(IDebugProcess2 process) {
            Debug.WriteLine("PythonEngine CanTerminateProcess");

            AssertMainThread();
            Debug.Assert(_events != null);
            Debug.Assert(_process != null);

            int processId = EngineUtils.GetProcessId(process);

            if (processId == _process.Id) {
                return VSConstants.S_OK;
            } else {
                return VSConstants.S_FALSE;
            }
        }

        // Launches a process by means of the debug engine.
        // Normally, Visual Studio launches a program using the IDebugPortEx2::LaunchSuspended method and then attaches the debugger 
        // to the suspended program. However, there are circumstances in which the debug engine may need to launch a program 
        // (for example, if the debug engine is part of an interpreter and the program being debugged is an interpreted language), 
        // in which case Visual Studio uses the IDebugEngineLaunch2::LaunchSuspended method
        // The IDebugEngineLaunch2::ResumeProcess method is called to start the process after the process has been successfully launched in a suspended state.
        int IDebugEngineLaunch2.LaunchSuspended(string pszServer, IDebugPort2 port, string exe, string args, string dir, string env, string options, enum_LAUNCH_FLAGS launchFlags, uint hStdInput, uint hStdOutput, uint hStdError, IDebugEventCallback2 ad7Callback, out IDebugProcess2 process) {
            Debug.WriteLine("--------------------------------------------------------------------------------");
            Debug.WriteLine("PythonEngine LaunchSuspended Begin " + launchFlags + " " + GetHashCode());
            AssertMainThread();
            Debug.Assert(_events == null);
            Debug.Assert(_process == null);
            Debug.Assert(_ad7ProgramId == Guid.Empty);

            process = null;
            
            _events = ad7Callback;

            PythonLanguageVersion version = DefaultVersion;
            PythonDebugOptions debugOptions = PythonDebugOptions.None;
            List<string[]> dirMapping = null;
            string interpreterOptions = null;
            if (options != null) {
                var splitOptions = SplitOptions(options);
                
                foreach (var optionSetting in splitOptions) {
                    var setting = optionSetting.Split(new[] { '=' }, 2);

                    if (setting.Length == 2) {
                        switch (setting[0]) {
                            case VersionSetting: version = GetLanguageVersion(setting[1]); break;
                            case WaitOnAbnormalExitSetting:
                                bool value;
                                if (Boolean.TryParse(setting[1], out value) &amp;&amp; value) {
                                    debugOptions |= PythonDebugOptions.WaitOnAbnormalExit;
                                }
                                break;
                            case WaitOnNormalExitSetting:
                                if (Boolean.TryParse(setting[1], out value) &amp;&amp; value) {
                                    debugOptions |= PythonDebugOptions.WaitOnNormalExit;
                                }
                                break;
                            case RedirectOutputSetting:
                                if (Boolean.TryParse(setting[1], out value)) {
                                    debugOptions |= PythonDebugOptions.RedirectOutput;
                                }
                                break;
                            case DirMappingSetting:
                                string[] dirs = setting[1].Split('|');
                                if (dirs.Length == 2) {
                                    if (dirMapping == null) {
                                        dirMapping = new List<string[]>();
                                    }
                                    Debug.WriteLine(String.Format("Mapping dir {0} to {1}", dirs[0], dirs[1]));
                                    dirMapping.Add(dirs);
                                }
                                break;
                            case InterpreterOptions:
                                interpreterOptions = setting[1];
                                break;
                        }
                    }
                }
            }

            _process = new PythonProcess(version, exe, args, dir, env, interpreterOptions, debugOptions, dirMapping);

            AttachEvents(_process);

            _programCreated = false;
            _loadComplete.Reset();

            _process.Start();

            AD_PROCESS_ID adProcessId = new AD_PROCESS_ID();
            adProcessId.ProcessIdType = (uint)enum_AD_PROCESS_ID.AD_PROCESS_ID_SYSTEM;
            adProcessId.dwProcessId = (uint)_process.Id;

            EngineUtils.RequireOk(port.GetProcess(adProcessId, out process));
            Debug.WriteLine("PythonEngine LaunchSuspended returning S_OK");
            Debug.Assert(process != null);
            Debug.Assert(!_process.HasExited);

            return VSConstants.S_OK;
        }

        private static string[] SplitOptions(string options) {
            List<string> res = new List<string>();
            int lastStart = 0;
            for (int i = 0; i &lt; options.Length; i++) {
                if (options[i] == ';') {
                    if (i &lt; options.Length - 1 &amp;&amp; options[i + 1] != ';') {
                        // valid option boundary
                        res.Add(options.Substring(lastStart, i - lastStart));
                        lastStart = i + 1;
                    } else {
                        i++;
                    }
                }
            }
            if (options.Length  - lastStart &gt; 0) {
                res.Add(options.Substring(lastStart, options.Length - lastStart));
            }
            return res.ToArray();
        }

        // Default version, we never really use this because we always provide the version, but if someone
        // else started our debugger they could choose not to provide the version.
        private const PythonLanguageVersion DefaultVersion = PythonLanguageVersion.V27;

        private static PythonLanguageVersion GetLanguageVersion(string options) {
            PythonLanguageVersion langVersion;
            if (options == null || !Enum.TryParse<pythonlanguageversion>(options, out langVersion)) {
                langVersion = DefaultVersion;
            }
            return langVersion;
        }

        // Resume a process launched by IDebugEngineLaunch2.LaunchSuspended
        int IDebugEngineLaunch2.ResumeProcess(IDebugProcess2 process) {
            Debug.WriteLine("Python Debugger ResumeProcess Begin");

            AssertMainThread();
            if (_events == null) {
                // process failed to start
                Debug.WriteLine("ResumeProcess fails, no events");
                return VSConstants.E_FAIL;
            }

            Debug.Assert(_events != null);
            Debug.Assert(_process != null);
            Debug.Assert(_process != null);
            Debug.Assert(_ad7ProgramId == Guid.Empty);

            int processId = EngineUtils.GetProcessId(process);

            if (processId != _process.Id) {
                Debug.WriteLine("ResumeProcess fails, wrong process");
                return VSConstants.S_FALSE;
            }

            // Send a program node to the SDM. This will cause the SDM to turn around and call IDebugEngine2.Attach
            // which will complete the hookup with AD7
            IDebugPort2 port;
            EngineUtils.RequireOk(process.GetPort(out port));

            IDebugDefaultPort2 defaultPort = (IDebugDefaultPort2)port;

            IDebugPortNotify2 portNotify;
            EngineUtils.RequireOk(defaultPort.GetPortNotify(out portNotify));

            EngineUtils.RequireOk(portNotify.AddProgramNode(new AD7ProgramNode(_process.Id)));

            if (_ad7ProgramId == Guid.Empty) {
                Debug.WriteLine("ResumeProcess fails, empty program guid");
                Debug.Fail("Unexpected problem -- IDebugEngine2.Attach wasn't called");
                return VSConstants.E_FAIL;
            }

            Debug.WriteLine("ResumeProcess return S_OK");
            return VSConstants.S_OK;
        }

        // This function is used to terminate a process that the engine launched
        // The debugger will call IDebugEngineLaunch2::CanTerminateProcess before calling this method.
        int IDebugEngineLaunch2.TerminateProcess(IDebugProcess2 process) {
            Debug.WriteLine("PythonEngine TerminateProcess");

            AssertMainThread();
            Debug.Assert(_events != null);
            Debug.Assert(_process != null);

            int processId = EngineUtils.GetProcessId(process);
            if (processId != _process.Id) {
                return VSConstants.S_FALSE;
            }

            _process.Terminate();

            return VSConstants.S_OK;
        }

        #endregion

        #region IDebugProgram2 Members

        // Determines if a debug engine (DE) can detach from the program.
        public int CanDetach() {
            if (_attached) {
                return VSConstants.S_OK;
            }
            return VSConstants.S_FALSE;
        }

        // The debugger calls CauseBreak when the user clicks on the pause button in VS. The debugger should respond by entering
        // breakmode. 
        public int CauseBreak() {
            Debug.WriteLine("PythonEngine CauseBreak");
            AssertMainThread();

            _process.Break();

            return VSConstants.S_OK;
        }

        // Continue is called from the SDM when it wants execution to continue in the debugee
        // but have stepping state remain. An example is when a tracepoint is executed, 
        // and the debugger does not want to actually enter break mode.
        public int Continue(IDebugThread2 pThread) {
            Debug.WriteLine("PythonEngine Continue");
            AssertMainThread();

            AD7Thread thread = (AD7Thread)pThread;

            // TODO: How does this differ from ExecuteOnThread?
            thread.GetDebuggedThread().Resume();

            return VSConstants.S_OK;
        }

        // Detach is called when debugging is stopped and the process was attached to (as opposed to launched)
        // or when one of the Detach commands are executed in the UI.
        public int Detach() {
            Debug.WriteLine("PythonEngine Detach");
            AssertMainThread();

            _breakpointManager.ClearBoundBreakpoints();

            _process.Detach();

            return VSConstants.S_OK;
        }

        // Enumerates the code contexts for a given position in a source file.
        public int EnumCodeContexts(IDebugDocumentPosition2 pDocPos, out IEnumDebugCodeContexts2 ppEnum) {
            string filename;
            pDocPos.GetFileName(out filename);
            TEXT_POSITION[] beginning = new TEXT_POSITION[1], end = new TEXT_POSITION[1];

            pDocPos.GetRange(beginning, end);

            ppEnum = new AD7CodeContextEnum(new[] { new AD7MemoryAddress(this, filename, (uint)beginning[0].dwLine) });
            return VSConstants.S_OK;
        }

        // EnumCodePaths is used for the step-into specific feature -- right click on the current statment and decide which
        // function to step into. This is not something that we support.
        public int EnumCodePaths(string hint, IDebugCodeContext2 start, IDebugStackFrame2 frame, int fSource, out IEnumCodePaths2 pathEnum, out IDebugCodeContext2 safetyContext) {
            pathEnum = null;
            safetyContext = null;
            return VSConstants.E_NOTIMPL;
        }

        // EnumModules is called by the debugger when it needs to enumerate the modules in the program.
        public int EnumModules(out IEnumDebugModules2 ppEnum) {
            AssertMainThread();


            AD7Module[] moduleObjects = new AD7Module[_modules.Count];
            int i = 0;
            foreach (var keyValue in _modules) {
                var module = keyValue.Key;
                var adModule = keyValue.Value;

                moduleObjects[i++] = adModule;
            }

            ppEnum = new AD7ModuleEnum(moduleObjects);

            return VSConstants.S_OK;
        }

        // EnumThreads is called by the debugger when it needs to enumerate the threads in the program.
        public int EnumThreads(out IEnumDebugThreads2 ppEnum) {
            AssertMainThread();

            AD7Thread[] threadObjects = new AD7Thread[_threads.Count];
            int i = 0;
            foreach (var keyValue in _threads) {
                var thread = keyValue.Key;
                var adThread = keyValue.Value;

                Debug.Assert(adThread != null);
                threadObjects[i++] = adThread;
            }

            ppEnum = new AD7ThreadEnum(threadObjects);

            return VSConstants.S_OK;
        }

        // The properties returned by this method are specific to the program. If the program needs to return more than one property, 
        // then the IDebugProperty2 object returned by this method is a container of additional properties and calling the 
        // IDebugProperty2::EnumChildren method returns a list of all properties.
        // A program may expose any number and type of additional properties that can be described through the IDebugProperty2 interface. 
        // An IDE might display the additional program properties through a generic property browser user interface.
        public int GetDebugProperty(out IDebugProperty2 ppProperty) {
            throw new Exception("The method or operation is not implemented.");
        }

        // The debugger calls this when it needs to obtain the IDebugDisassemblyStream2 for a particular code-context.
        public int GetDisassemblyStream(enum_DISASSEMBLY_STREAM_SCOPE dwScope, IDebugCodeContext2 codeContext, out IDebugDisassemblyStream2 disassemblyStream) {
            disassemblyStream = null;
            return VSConstants.E_NOTIMPL;
        }

        // This method gets the Edit and Continue (ENC) update for this program. A custom debug engine always returns E_NOTIMPL
        public int GetENCUpdate(out object update) {
            update = null;
            return VSConstants.S_OK;
        }

        // Gets the name and identifier of the debug engine (DE) running this program.
        public int GetEngineInfo(out string engineName, out Guid engineGuid) {
            engineName = "Python Engine";
            engineGuid = new Guid(DebugEngineId);
            return VSConstants.S_OK;
        }

        // The memory bytes as represented by the IDebugMemoryBytes2 object is for the program's image in memory and not any memory 
        // that was allocated when the program was executed.
        public int GetMemoryBytes(out IDebugMemoryBytes2 ppMemoryBytes) {
            throw new Exception("The method or operation is not implemented.");
        }

        // Gets the name of the program.
        // The name returned by this method is always a friendly, user-displayable name that describes the program.
        public int GetName(out string programName) {
            // The engine uses default transport and doesn't need to customize the name of the program,
            // so return NULL.
            programName = null;
            return VSConstants.S_OK;
        }

        // Gets a GUID for this program. A debug engine (DE) must return the program identifier originally passed to the IDebugProgramNodeAttach2::OnAttach
        // or IDebugEngine2::Attach methods. This allows identification of the program across debugger components.
        public int GetProgramId(out Guid guidProgramId) {
            guidProgramId = _ad7ProgramId;
            return guidProgramId == Guid.Empty ? VSConstants.E_FAIL : VSConstants.S_OK;
        }

        // This method is deprecated. Use the IDebugProcess3::Step method instead.
        public int Step(IDebugThread2 pThread, enum_STEPKIND sk, enum_STEPUNIT Step) {
            var thread = ((AD7Thread)pThread).GetDebuggedThread();
            switch (sk) {
                case enum_STEPKIND.STEP_INTO: thread.StepInto(); break;
                case enum_STEPKIND.STEP_OUT: thread.StepOut(); break;
                case enum_STEPKIND.STEP_OVER: thread.StepOver(); break; 
            }
            return VSConstants.S_OK;
        }

        // Terminates the program.
        public int Terminate() {
            Debug.WriteLine("PythonEngine Terminate");
            // Because we implement IDebugEngineLaunch2 we will terminate
            // the process in IDebugEngineLaunch2.TerminateProcess
            return VSConstants.S_OK;
        }

        // Writes a dump to a file.
        public int WriteDump(enum_DUMPTYPE DUMPTYPE, string pszDumpUrl) {
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        #region IDebugProgram3 Members

        // ExecuteOnThread is called when the SDM wants execution to continue and have 
        // stepping state cleared.  See http://msdn.microsoft.com/en-us/library/bb145596.aspx for a
        // description of different ways we can resume.
        public int ExecuteOnThread(IDebugThread2 pThread) {
            AssertMainThread();

            // clear stepping state on the thread the user was currently on
            AD7Thread thread = (AD7Thread)pThread;
            thread.GetDebuggedThread().ClearSteppingState();

            _process.Resume();

            return VSConstants.S_OK;
        }

        #endregion

        #region IDebugSymbolSettings100 members

        public int SetSymbolLoadState(int bIsManual, int bLoadAdjacent, string strIncludeList, string strExcludeList) {
            // The SDM will call this method on the debug engine when it is created, to notify it of the user's
            // symbol settings in Tools-&gt;Options-&gt;Debugging-&gt;Symbols.
            //
            // Params:
            // bIsManual: true if 'Automatically load symbols: Only for specified modules' is checked
            // bLoadAdjacent: true if 'Specify modules'-&gt;'Always load symbols next to the modules' is checked
            // strIncludeList: semicolon-delimited list of modules when automatically loading 'Only specified modules'
            // strExcludeList: semicolon-delimited list of modules when automatically loading 'All modules, unless excluded'

            return VSConstants.S_OK;
        }

        #endregion

        #region Deprecated interface methods
        // These methods are not called by the Visual Studio debugger, so they don't need to be implemented

        int IDebugEngine2.EnumPrograms(out IEnumDebugPrograms2 programs) {
            Debug.Fail("This function is not called by the debugger");

            programs = null;
            return VSConstants.E_NOTIMPL;
        }

        public int Attach(IDebugEventCallback2 pCallback) {
            Debug.Fail("This function is not called by the debugger");

            return VSConstants.E_NOTIMPL;
        }

        public int GetProcess(out IDebugProcess2 process) {
            Debug.Fail("This function is not called by the debugger");

            process = null;
            return VSConstants.E_NOTIMPL;
        }

        public int Execute() {
            Debug.Fail("This function is not called by the debugger.");
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        #region Events

        internal void Send(IDebugEvent2 eventObject, string iidEvent, IDebugProgram2 program, IDebugThread2 thread) {
            uint attributes;
            Guid riidEvent = new Guid(iidEvent);

            EngineUtils.RequireOk(eventObject.GetAttributes(out attributes));

            Debug.WriteLine(String.Format("Sending Event: {0} {1}", eventObject.GetType(), iidEvent));
            try {
                EngineUtils.RequireOk(_events.Event(this, null, program, thread, eventObject, ref riidEvent, attributes));
            } catch (InvalidCastException) {                
                // COM object has gone away
            }
        }

        internal void Send(IDebugEvent2 eventObject, string iidEvent, IDebugThread2 thread) {
            Send(eventObject, iidEvent, this, thread);
        }

        private void AttachEvents(PythonProcess _process) {
            _process.ProcessLoaded += OnProcessLoaded;
            _process.ModuleLoaded += OnModuleLoaded;
            _process.ThreadCreated += OnThreadCreated;

            _process.BreakpointBindFailed += OnBreakpointBindFailed;
            _process.BreakpointBindSucceeded += OnBreakpointBindSucceeded;

            _process.BreakpointHit += OnBreakpointHit;
            _process.AsyncBreakComplete += OnAsyncBreakComplete;
            _process.ExceptionRaised += OnExceptionRaised;
            _process.ProcessExited += OnProcessExited;
            _process.StepComplete += OnStepComplete;
            _process.ThreadExited += OnThreadExited;
            _process.DebuggerOutput += OnDebuggerOutput;
        }

        private void OnThreadExited(object sender, ThreadEventArgs e) {
            // TODO: Thread exit code
            var oldThread = _threads[e.Thread];
            _threads.Remove(e.Thread);

            Send(new AD7ThreadDestroyEvent(0), AD7ThreadDestroyEvent.IID, oldThread);
        }

        private void OnThreadCreated(object sender, ThreadEventArgs e) {
            var newThread = new AD7Thread(this, e.Thread);
            _threads.Add(e.Thread, newThread);

            lock (_syncLock) {
                if (_programCreated) {
                    SendThreadStart(newThread);
                } else {
                    _startThread = newThread;
                }
            }
        }

        private void OnStepComplete(object sender, ThreadEventArgs e) {
            Send(new AD7SteppingCompleteEvent(), AD7SteppingCompleteEvent.IID, _threads[e.Thread]);
        }

        private void OnProcessLoaded(object sender, ThreadEventArgs e) {
            lock (_syncLock) {
                if (_programCreated) {
                    // we've delviered the program created event, deliver the load complete event
                    SendLoadComplete(_threads[e.Thread]);
                } else {
                    Debug.WriteLine("Delaying load complete " + GetHashCode());
                    // we haven't delivered the program created event, wait until we do to deliver the process loaded event.
                    _processLoadedThread = _threads[e.Thread];
                }
            }
        }

        private void OnProcessExited(object sender, ProcessExitedEventArgs e) {
            try {
                Send(new AD7ProgramDestroyEvent((uint)e.ExitCode), AD7ProgramDestroyEvent.IID, null);
            } catch (InvalidOperationException) {
                // we can race at shutdown and deliver the event after the debugger is shutting down.
            }
        }

        private void OnModuleLoaded(object sender, ModuleLoadedEventArgs e) {
            lock (_syncLock) {
                var adModule = _modules[e.Module] = new AD7Module(e.Module);
                if (_programCreated) {
                    SendModuleLoaded(adModule);
                } else {
                    _startModule = adModule;
                }
            }
        }

        private void OnExceptionRaised(object sender, ExceptionRaisedEventArgs e) {
            // Exception events are sent when an exception occurs in the debuggee that the debugger was not expecting.
            Send(
                new AD7DebugExceptionEvent(e.Exception.TypeName + Environment.NewLine + e.Exception.Description),
                AD7DebugExceptionEvent.IID,
                _threads[e.Thread]
            );
        }

        private void OnBreakpointHit(object sender, BreakpointHitEventArgs e) {
            var boundBreakpoints = new[] { _breakpointManager.GetBreakpoint(e.Breakpoint) };

            // An engine that supports more advanced breakpoint features such as hit counts, conditions and filters
            // should notify each bound breakpoint that it has been hit and evaluate conditions here.

            Send(new AD7BreakpointEvent(new AD7BoundBreakpointsEnum(boundBreakpoints)), AD7BreakpointEvent.IID, _threads[e.Thread]);
        }

        private void OnBreakpointBindSucceeded(object sender, BreakpointEventArgs e) {
            IDebugPendingBreakpoint2 pendingBreakpoint;
            var boundBreakpoint = _breakpointManager.GetBreakpoint(e.Breakpoint);
            ((IDebugBoundBreakpoint2)boundBreakpoint).GetPendingBreakpoint(out pendingBreakpoint);

            Send(
                new AD7BreakpointBoundEvent((AD7PendingBreakpoint)pendingBreakpoint, boundBreakpoint),
                AD7BreakpointBoundEvent.IID,
                null
            );
        }

        private void OnBreakpointBindFailed(object sender, BreakpointEventArgs e) {
        }

        private void OnAsyncBreakComplete(object sender, ThreadEventArgs e) {
            AD7Thread thread;
            if (!_threads.TryGetValue(e.Thread, out thread)) {
                _threads[e.Thread] = thread = new AD7Thread(this, e.Thread);
            }
            Send(new AD7AsyncBreakCompleteEvent(), AD7AsyncBreakCompleteEvent.IID, thread);
        }

        private void OnDebuggerOutput(object sender, OutputEventArgs e) {
            AD7Thread thread;
            if (!_threads.TryGetValue(e.Thread, out thread)) {
                _threads[e.Thread] = thread = new AD7Thread(this, e.Thread);
            }

            Send(new AD7DebugOutputStringEvent2(e.Output), AD7DebugOutputStringEvent2.IID, thread);
        }

        #endregion
    }
}
