// SafeRegistryHandle.cs
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
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32.SafeHandles;

namespace CL.RegAsm
{
    /// <summary>
    /// Safe handle used for a registry key handle, e.g. HKEY. Literally copied
    /// from the .NET Framework 4 which supports this natively.
    /// </summary>
    [SecurityCritical]
    public sealed class SafeRegistryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        /// Win32 API to close a registry key handle.
        [SuppressUnmanagedCodeSecurity]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [DllImport("advapi32.dll")]
        private static extern int RegCloseKey(IntPtr hKey);

        /// <summary>
        /// Constructor with pre-existing handle.
        /// </summary>
        /// <param name="existingHandle">Registry handle to wrap.</param>
        /// <param name="assumeOwnership">Whether to assume ownership of the
        /// <paramref name="existingHandle"/>.</param>
        [SecurityCritical]
        public SafeRegistryHandle(IntPtr existingHandle, bool assumeOwnership)
            : base(assumeOwnership)
        {
            SetHandle(existingHandle);
        }

        /// <summary>
        /// Called to release our registry handle.
        /// </summary>
        /// <returns><c>true</c> if the handle was successfully released.</returns>
        [SecurityCritical]
        protected override bool ReleaseHandle()
        {
            return RegCloseKey(handle) == 0;
        }
    }
}
