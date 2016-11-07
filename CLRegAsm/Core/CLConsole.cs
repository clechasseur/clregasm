// CLConsole.cs
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
using System.IO;

namespace CL.RegAsm
{
    /// <summary>
    /// Semi-equivalent of <see cref="System.Console"/> for CLRegAsm.
    /// Uses a <see cref="PreSandboxParams"/> object to determine what
    /// to output.
    /// </summary>
    internal sealed class CLConsole
    {
        /// <summary>
        /// <see cref="System.IO.TextWriter"/> to use to write verbose
        /// messages to the console.
        /// </summary>
        public TextWriter Verbose
        {
            get;
            private set;
        }

        /// <summary>
        /// <see cref="System.IO.TextWriter"/> to use to write standard
        /// messages to the console.
        /// </summary>
        public TextWriter Out
        {
            get;
            private set;
        }

        /// <summary>
        /// <see cref="System.IO.TextWriter"/> to use to write error
        /// messages to the console's error stream.
        /// </summary>
        public TextWriter Error
        {
            get;
            private set;
        }

        /// <summary>
        /// <see cref="System.IO.TextWriter"/> to use to write debug
        /// information to the console.
        /// </summary>
        public TextWriter Debug
        {
            get;
            private set;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="prms"><see cref="PreSandboxParams"/> object to
        /// use to determine what to output. Cannot be <c>null</c>.</param>
        public CLConsole(PreSandboxParams prms)
        {
            System.Diagnostics.Debug.Assert(prms != null);

            // Set streams according to params.
            this.Verbose = !prms.silent && prms.verbose ? Console.Out : TextWriter.Null;
            this.Out = !prms.silent ? Console.Out : TextWriter.Null;
            this.Error = Console.Error;
            this.Debug = prms.debug ? Console.Out : TextWriter.Null;
        }

        /// <summary>
        /// Writes a newline to the output stream.
        /// Same as calling <c>Out.WriteLine()</c>.
        /// </summary>
        public void WriteLine()
        {
            Out.WriteLine();
        }

        /// <summary>
        /// Writes a string value to the output stream.
        /// Same as calling <c>Out.WriteLine(value)</c>.
        /// </summary>
        /// <param name="value">Value to write.</param>
        public void WriteLine(string value)
        {
            Out.WriteLine(value);
        }

        /// <summary>
        /// Writes a formatted string to the output stream.
        /// Same as calling <c>Out.WriteLine(format, arg0)</c>.
        /// </summary>
        /// <param name="format">Format string.</param>
        /// <param name="arg0">Argument to insert in <paramref name="format"/>.</param>
        public void WriteLine(string format, object arg0)
        {
            Out.WriteLine(format, arg0);
        }

        /// <summary>
        /// Writes a formatted string to the output stream.
        /// Same as calling <c>Out.WriteLine(format, ...)</c>.
        /// </summary>
        /// <param name="format">Format string.</param>
        /// <param name="args">Arguments to insert in <paramref name="format"/>.</param>
        public void WriteLine(string format, params object[] args)
        {
            Out.WriteLine(format, args);
        }

        /// <summary>
        /// Prints details about an exception to the error stream.
        /// Includes stack trace in debug mode and recurses for
        /// inner exceptions.
        /// </summary>
        /// <param name="e"><see cref="System.Exception"/> to
        /// print. Cannot be <c>null</c>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="e"/>
        /// is <c>null</c>.</exception>
        public void PrintException(Exception e)
        {
            if (e == null) {
                throw new ArgumentNullException("e");
            }

            Error.WriteLine("Error: {0}", e.Message);
            Exception re = e;
            while (re != null) {
                Debug.WriteLine("Type: {0}", e.GetType().FullName);
                Debug.WriteLine("Stack trace:");
                Debug.WriteLine(re.StackTrace);
                re = re.InnerException;
                if (re != null) {
                    Error.WriteLine();
                    Error.WriteLine("Caused by: {0}", re.Message);
                }
            }
        }
    }
}
