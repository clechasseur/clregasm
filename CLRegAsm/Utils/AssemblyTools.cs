// AssemblyTools.cs
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
using System.Reflection;

namespace CL.RegAsm.Utils
{
    /// <summary>
    /// Static class containing helpers dealing with <see cref="Assembly"/>.
    /// </summary>
    internal static class AssemblyTools
    {
        /// <summary>
        /// Returns the full path to the file containing the given <see cref="Assembly"/>.
        /// </summary>
        /// <param name="assembly"><see cref="Assembly"/> to find.</param>
        /// <returns>Full path to file containing assembly.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is <c>null</c>.</exception>
        /// <remarks>
        /// This function exists because I tend to forget how to get that path.
        /// Now I will only forget that I have this helper (one step at a time).
        /// </remarks>
        public static string GetAssemblyPath(Assembly assembly)
        {
            // Validate input.
            if (assembly == null) {
                throw new ArgumentNullException("assembly");
            }

            // Use Uri to get local path of CodeBase.
            return new Uri(assembly.CodeBase).LocalPath;
        }
    }
}
