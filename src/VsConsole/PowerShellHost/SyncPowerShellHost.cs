﻿using System;
using System.Management.Automation;
using NuGet.VisualStudio;

namespace NuGetConsole.Host.PowerShell.Implementation {
    internal class SyncPowerShellHost : PowerShellHost {
        public SyncPowerShellHost(string name, IRunspaceManager runspaceManager)
            : base(name, runspaceManager) {
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        protected override bool ExecuteHost(string fullCommand, string command, params object[] inputs) {
            DateTime startExecutionTime = DateTime.Now;
            SetSyncModeOnHost(true);
            try {
                Runspace.Invoke(fullCommand, inputs, true);
            }
            catch (RuntimeException e) {
                ReportError(e.ErrorRecord);
                ExceptionHelper.WriteToActivityLog(e);
            }
            catch (Exception e) {
                ReportError(e);
                ExceptionHelper.WriteToActivityLog(e);
            }

            Runspace.AddHistory(command, startExecutionTime);
            return true;
        }
    }
}