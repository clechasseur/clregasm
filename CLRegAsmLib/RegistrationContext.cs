// RegistrationContext.cs
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

namespace CL.RegAsm.Lib
{
    /// <summary>
    /// Static class allowing a class library to query the current registration context.
    /// If the class library is being registered using CLRegAsm, properties such as whether
    /// it is a per-user registration are available.
    /// </summary>
    /// <remarks>
    /// This class is safe to use even if the class library isn't being registered by CLRegAsm.
    /// In this case, the registration context will behave like a standard registration.
    /// </remarks>
    public static class RegistrationContext
    {
        /// <summary>
        /// Environment variable set by CLRegAsm to identify itself during registration.
        /// </summary>
        /// <seealso cref="CLREGASM_ENV_VARS_VALUE"/>
        internal const string CLREGASM_ID_ENV_VAR = "CLREGASM_ID";

        /// <summary>
        /// Environment variable set by CLRegAsm during a per-user registration.
        /// </summary>
        /// <seealso cref="REGASM_PER_USER_ENV_VAR"/>
        /// <seealso cref="CLREGASM_ENV_VARS_VALUE"/>
        internal const string CLREGASM_PER_USER_ENV_VAR = "CLREGASM_PERUSER";

        /// <summary>
        /// Environment variable set by CLRegAsm during a per-user registration.
        /// This is a <i>tool-neutral</i> version in case it ends up being
        /// supported by other tools, like Microsoft's own RegAsm.
        /// </summary>
        /// <seealso cref="CLREGASM_PER_USER_ENV_VAR"/>
        /// <seealso cref="CLREGASM_ENV_VARS_VALUE"/>
        internal const string REGASM_PER_USER_ENV_VAR = "REGASM_PERUSER";

        /// <summary>
        /// Array containing all environment variables set by CLRegAsm during
        /// a per-user registration.
        /// </summary>
        /// <seealso cref="CLREGASM_PER_USER_ENV_VAR"/>
        /// <seealso cref="REGASM_PER_USER_ENV_VAR"/>
        /// <seealso cref="CLREGASM_ENV_VARS_VALUE"/>
        internal static readonly string[] CLREGASM_PER_USER_ENV_VARS = new string[] {
            CLREGASM_PER_USER_ENV_VAR,
            REGASM_PER_USER_ENV_VAR,
        };

        /// <summary>
        /// Value set for all environment variables set by CLRegAsm.
        /// </summary>
        internal const string CLREGASM_ENV_VARS_VALUE = "1";

        /// <summary>
        /// Checks whether the class library is currently being registered by CLRegAsm.
        /// </summary>
        public static bool IsCLRegAsmRegistration
        {
            get {
                return Environment.GetEnvironmentVariable(CLREGASM_ID_ENV_VAR) != null;
            }
        }

        /// <summary>
        /// Checks whether the class library is current being registered for the current
        /// user only. Class library is responsible for redirecting registry entries,
        /// configuration files, etc. as appropriate.
        /// </summary>
        public static bool IsPerUserRegistration
        {
            get {
                bool perUser = false;
                foreach (string envVar in CLREGASM_PER_USER_ENV_VARS) {
                    if (Environment.GetEnvironmentVariable(envVar) != null) {
                        perUser = true;
                        break;
                    }
                }
                return perUser;
            }
        }
    }
}
