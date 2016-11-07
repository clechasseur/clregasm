// TestInterface.cs
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
using System.Runtime.InteropServices;
using CL.RegAsm.Lib;
using Microsoft.Win32;

namespace CL.RegAsm.Test.TestAssembly
{
    /// <summary>
    /// Test class that will be exposed to COM. Used to test CLRegAsm.
    /// </summary>
    [Guid("8C9CCC44-4FEF-4C49-B8D6-3BD8D86F2574"), ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class TestClass : ITestInterface
    {
        /// Marker key that we'll use to prove we registered.
        private const string MARKER_KEY = @"Software\clechasseur\CLRegAsmTestAssembly\Marker";

        #region ITestInterface Members

        /// <summary>
        /// Returns the name of this class.
        /// </summary>
        public string Name
        {
            get {
                return typeof(TestClass).Name;
            }
        }

        /// <summary>
        /// Adds two numbers.
        /// </summary>
        /// <param name="a">First number to add.</param>
        /// <param name="b">Second number to add.</param>
        /// <returns>The sum of <paramref name="a"/> and <paramref name="b"/>.</returns>
        public int Add(int a, int b)
        {
            return a + b;
        }

        #endregion

        /// <summary>
        /// Method called during assembly COM registration. We can use this opportunity
        /// to test various features of CLRegAsm.
        /// </summary>
        /// <param name="t">Type being registered; unused.</param>
        [ComRegisterFunction]
        private static void Register(Type t)
        {
            // Get registry key depending on registration context.
            RegistryKey key;
            if (RegistrationContext.IsPerUserRegistration) {
                key = Registry.CurrentUser;
            } else {
                key = Registry.LocalMachine;
            }

            // Create a marker key.
            using (RegistryKey subKey = key.CreateSubKey(MARKER_KEY)) {
            }
        }

        /// <summary>
        /// Method called during assembly COM unregistration. We can use this opportunity
        /// to test various features of CLRegAsm.
        /// </summary>
        /// <param name="t">Type being unregistered.</param>
        [ComUnregisterFunction]
        private static void Unregister(Type t)
        {
            // Get registry key depending on registration context.
            RegistryKey key;
            if (RegistrationContext.IsPerUserRegistration) {
                key = Registry.CurrentUser;
            } else {
                key = Registry.LocalMachine;
            }

            // Delete the marker key.
            try {
                key.DeleteSubKeyTree(MARKER_KEY);
            } catch (ArgumentNullException) {
                throw;
            } catch (ArgumentException) {
                // Key doesn't exist, user unregistered twice. No biggie.
            }
        }
    }
}
