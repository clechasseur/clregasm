// Context.cs
// (c) 2016, Charles Lechasseur
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using CL.RegAsm.Lib;
using Microsoft.Win32.SafeHandles; // Needed for .NET 4

namespace CL.RegAsm
{
    /// <summary>
    /// Class responsible for setting up registration/unregistration context
    /// in CLRegAsm. Mainly used to set up per-user registration.
    /// </summary>
    internal sealed class Context : IDisposable
    {
        /// Numeric value of the HKEY_CLASSES_ROOT constant from Win32 headers.
        private const int HKEY_CLASSES_ROOT = Int32.MinValue;

        /// Numeric value of the HKEY_CURRENT_USER constant from Win32 headers.
        private const int HKEY_CURRENT_USER = -2147483647;

        /// Possible values to pass as desired security when opening or
        /// creating a registry key using Win32 APIs.
        [Flags]
        private enum RegSAM
        {
            QueryValue = 0x0001,
            SetValue = 0x0002,
            CreateSubKey = 0x0004,
            EnumerateSubKeys = 0x0008,
            Notify = 0x0010,
            CreateLink = 0x0020,
            WOW64_32Key = 0x0200,
            WOW64_64Key = 0x0100,
            WOW64_Res = 0x0300,
            Read = 0x00020019,
            Write = 0x00020006,
            Execute = 0x00020019,
            AllAccess = 0x000f003f
        }

        /// Win32 API to open or create a registry key. Some parameters are left
        /// undefined since we don't need them in this class.
        [DllImport("advapi32.dll", CharSet=CharSet.Auto)]
        private static extern int RegCreateKeyEx(int hRoot, string subKey, int reserved,
             string keyClass, int options, RegSAM desiredRights, IntPtr securityAttributes,
             out SafeRegistryHandle hResultingKey, out int disposition);

        /// Win32 API to open an existing registry key.
        [DllImport("advapi32.dll", CharSet=CharSet.Auto)]
        private static extern int RegOpenKeyEx(int hRoot, string subKey, int options,
            RegSAM desiredSecurity, out SafeRegistryHandle hResultingKey);

        /// Win32 API to override a root key with another.
        [DllImport("advapi32.dll")]
        private static extern int RegOverridePredefKey(SafeRegistryHandle hKey, SafeRegistryHandle hNewKey);

        /// Handle of root registry key that was overridden.
        private SafeRegistryHandle hOverriddenRoot;

        /// Environment variables to remove when rewinding context.
        private List<string> envVarsToRemove = new List<string>();

        /// <summary>
        /// Constructor. Prepares the context according to the flags received.
        /// </summary>
        /// <param name="perUser">Whether to perform per-user (<c>true</c>) or
        /// per-machine (<c>false</c>) registration/unregistration.</param>
        /// <exception cref="Win32Exception">An error occured during
        /// a registry API call.</exception>
        public Context(bool perUser)
        {
            // For per-user registration/unregistration, we override
            // HKEY_CLASSES_ROOT with HKEY_CURRENT_USER\Software\Classes.
            if (perUser) {
                int err = RegOpenKeyEx(HKEY_CLASSES_ROOT, String.Empty, 0,
                    RegSAM.QueryValue | RegSAM.SetValue | RegSAM.CreateSubKey, out hOverriddenRoot);
                if (err == 0 && !hOverriddenRoot.IsInvalid) {
                    SafeRegistryHandle hUserSoftwareClassesKey = null;
                    try {
                        int disposition;
                        err = RegCreateKeyEx(HKEY_CURRENT_USER, @"Software\Classes", 0, null, 0,
                            RegSAM.QueryValue | RegSAM.SetValue | RegSAM.CreateSubKey,
                            IntPtr.Zero, out hUserSoftwareClassesKey, out disposition);
                        if (err == 0 && !hUserSoftwareClassesKey.IsInvalid) {
                            err = RegOverridePredefKey(hOverriddenRoot, hUserSoftwareClassesKey);
                            if (err != 0) {
                                throw new Win32Exception(err);
                            }
                        } else {
                            throw new Win32Exception(err);
                        }
                    } finally {
                        if (hUserSoftwareClassesKey != null) {
                            hUserSoftwareClassesKey.Dispose();
                        }
                    }
                } else {
                    throw new Win32Exception(err);
                }

                // Also set some environment variables if they don't already exist.
                // This will allow assemblies that have COM registration/unregistration
                // function to know it's done in a per-user context, since we have no
                // way of communicating with the assembly beforehand.
                foreach (string envVar in RegistrationContext.CLREGASM_PER_USER_ENV_VARS) {
                    if (Environment.GetEnvironmentVariable(envVar) == null) {
                        Environment.SetEnvironmentVariable(envVar, RegistrationContext.CLREGASM_ENV_VARS_VALUE);
                        envVarsToRemove.Add(envVar);
                    }
                }
            }
        }

        #region IDisposable Members
        
        /// <summary>
        /// Cleans up the registration context.
        /// </summary>
        public void Dispose()
        {
            // If we overrode a registry key in constructor, restore it.
            if (hOverriddenRoot != null) {
                RegOverridePredefKey(hOverriddenRoot, null);
                hOverriddenRoot.Dispose();
                hOverriddenRoot = null;
            }

            // If we added some environment variables, remove them.
            foreach (string envVar in envVarsToRemove) {
                Environment.SetEnvironmentVariable(envVar, null);
            }
        }

        #endregion
    }
}
