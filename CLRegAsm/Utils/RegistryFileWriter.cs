// RegistryFileWriter.cs
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
using System.Diagnostics;
using System.IO;

namespace CL.RegAsm.Utils
{
    /// <summary>
    /// Writer allowing the user to easily construct a registry import file
    /// (.reg) without having to know the specifics of the registry format.
    /// </summary>
    /// <remarks>
    /// This class is not thread-safe.
    /// </remarks>
    public sealed class RegistryFileWriter : IDisposable
    {
        /// <summary>
        /// Extension used by the shell for registry import files.
        /// </summary>
        public const string REGISTRY_FILE_EXTENSION = ".reg";

        /// Internal writer to write to the file.
        internal StreamWriter writer;

        /// Active key writer. There can be only one (or something).
        internal RegistryKeyWriter keyWriter;

        /// <summary>
        /// Constructor. Creates a registry import file to write to it.
        /// </summary>
        /// <param name="fileName">Name of the file to create. If no
        /// extension is specified, <c>.reg</c> will be appended. If the file
        /// already exists, it is overwritten.</param>
        /// <exception cref="ArgumentNullException"><paramref name="fileName"/>
        /// is <c>null</c>.</exception>
        public RegistryFileWriter(string fileName)
            : this(fileName, false)
        {
        }

        /// <summary>
        /// Constructor. Creates or opens a registry import file to write to it.
        /// </summary>
        /// <param name="fileName">Name of the file to create or open. If no
        /// extension is specified, <c>.reg</c> will be appended.</param>
        /// <param name="append">Whether to append to the file if it already
        /// exists. If set to <c>false</c> and the file already exists,
        /// it is overwritten.</param>
        /// <exception cref="ArgumentNullException"><paramref name="fileName"/>
        /// is <c>null</c>.</exception>
        public RegistryFileWriter(string fileName, bool append)
        {
            // Validate input.
            if (fileName == null) {
                throw new ArgumentNullException("fileName");
            }

            // Compute real filename, in case we need to add extension.
            string realFileName = fileName;
            if (String.IsNullOrEmpty(Path.GetExtension(fileName))) {
                realFileName += REGISTRY_FILE_EXTENSION;
            }

            // Create or open the file right away so that it fails fast if needed.
            writer = new StreamWriter(realFileName, append);

            // If this is a new file, write header.
            if (writer.BaseStream.Length == 0) {
                writer.WriteLine("REGEDIT4");
            }
        }

        /// <summary>
        /// Finalizer. If we reach this point, it means the object hasn't been
        /// disposed of, so we close the file here if needed.
        /// </summary>
        ~RegistryFileWriter()
        {
            if (writer != null) {
                writer.Close();
                writer = null;
            }
        }

        #region IDisposable Members

        /// <summary>
        /// Disposes of the object's resources right away. This finalizes the
        /// registry import file and closes it.
        /// </summary>
        public void Dispose()
        {
            if (writer != null) {
                writer.Close();
                writer = null;
            }
            GC.SuppressFinalize(this);
        }

        #endregion

        /// <summary>
        /// Adds a new key to the registry file and returns a writer allowing the
        /// user to add values to it.
        /// </summary>
        /// <param name="keyPath">Path to the registry key, including the root key.
        /// </param>
        /// <returns><see cref="RegistryKeyWriter"/> that allows the user to add
        /// values to the key. When done adding values to the key, call
        /// <see cref="RegistryKeyWriter.Dispose()"/> on the writer.</returns>
        /// <remarks>
        /// Only one <see cref="RegistryKeyWriter"/> can exist at a time, since
        /// you can only write one key at once to the registry import file. Make
        /// sure to dispose of the returned object before adding a new key.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="keyPath"/>
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="keyPath"/> is
        /// empty.</exception>
        /// <exception cref="ActiveRegistryKeyWriterException">There is still an
        /// active <see cref="RegistryKeyWriter"/> that must be disposed of before
        /// a new key can be added.</exception>
        public RegistryKeyWriter AddKey(string keyPath)
        {
            // Validate input.
            if (keyPath == null) {
                throw new ArgumentNullException("keyPath");
            }
            if (keyPath.Trim().Length == 0) {
                throw new ArgumentException("keyPath cannot be empty.", "keyPath");
            }

            // Make sure there are no active key writer.
            if (keyWriter != null) {
                throw new ActiveRegistryKeyWriterException();
            }

            // Create and return key writer. It will take care of registering
            // itself in our keyWriter member.
            return new RegistryKeyWriter(keyPath, this);
        }

        /// <summary>
        /// Adds a new empty key to the registry file. The key will contain no values.
        /// </summary>
        /// <param name="keyPath">Path to the registry key, including the root key.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="keyPath"/>
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="keyPath"/> is
        /// empty.</exception>
        /// <exception cref="ActiveRegistryKeyWriterException">There is still an
        /// active <see cref="RegistryKeyWriter"/> that must be disposed of before
        /// a new key can be added.</exception>
        public void AddEmptyKey(string keyPath)
        {
            RegistryKeyWriter keyWriter = AddKey(keyPath);
            Debug.Assert(keyWriter != null);
            keyWriter.Dispose();
        }
    }
    
    /// <summary>
    /// Writer allowing the user to add values to a registry key added to a
    /// <see cref="RegistryFileWriter"/>. After you're done adding values,
    /// dispose of the <see cref="RegistryKeyWriter"/> to make sure the key
    /// is closed and the <see cref="RegistryFileWriter"/> is ready to add
    /// another key.
    /// </summary>
    /// <seealso cref="RegistryFileWriter"/>
    public sealed class RegistryKeyWriter : IDisposable
    {
        /// Reference to our parent to call back when we're done.
        private RegistryFileWriter parent;

        /// Writer to use to write to the actual registry import file.
        private StreamWriter writer;

        /// <summary>
        /// Internal constructor called by <see cref="RegistryFileWriter"/> when
        /// a new key is added to the registry import file.
        /// </summary>
        /// <param name="keyPath">Path of the registry key, including root key.</param>
        /// <param name="parent">Parent <see cref="RegistryFileWriter"/> that
        /// created us. We need to call it back when we're done.</param>
        internal RegistryKeyWriter(string keyPath, RegistryFileWriter parent)
        {
            Debug.Assert(!String.IsNullOrEmpty(keyPath));
            Debug.Assert(parent != null);

            // Save reference to parent and its internal writer.
            this.parent = parent;
            this.writer = parent.writer;

            // Save reference to ourselves in parent to mark us as active.
            Debug.Assert(parent.keyWriter == null);
            parent.keyWriter = this;

            // Add a newline to the file to separate this key's values
            // from values of the previous key (or from the file header).
            this.writer.WriteLine();

            // Write the key header.
            this.writer.WriteLine("[{0}]", keyPath);
        }

        /// <summary>
        /// Finalizer. If we reach this point, we clean up as much as we can.
        /// </summary>
        ~RegistryKeyWriter()
        {
            Dispose(false);
        }

        #region IDisposable Members

        /// <summary>
        /// Disposes of this <see cref="RegistryKeyWriter"/>, finalizing the writing
        /// of the registry key and making it possible to add a new key via its
        /// parent <see cref="RegistryFileWriter"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        /// <summary>
        /// Adds a registry value to the current key in the registry import file.
        /// </summary>
        /// <param name="name">Name of the registry value to add. Cannot be
        /// <c>null</c> or an empty string.</param>
        /// <param name="value">Value to set for this registry value. Cannot be
        /// <c>null</c>, but can be an empty string.</param>
        /// <exception cref="ArgumentNullException">A required parameter was
        /// set to <c>null</c></exception>
        /// <exception cref="ArgumentException"><paramref name="name"/> was set
        /// to an empty string.</exception>
        public void AddValue(string name, string value)
        {
            // Validate input.
            if (name == null) {
                throw new ArgumentNullException("name");
            }
            if (value == null) {
                throw new ArgumentNullException("value");
            }
            if (name.Length == 0) {
                throw new ArgumentException("name cannot be empty.", "name");
            }

            InternalAddValue(name, value);
        }

        /// <summary>
        /// Adds the default registry value to the current key in the registry
        /// import file. The default key has no name and is identified as
        /// "(Default)" in regedit.
        /// </summary>
        /// <param name="value">Value to set for the default registry value.
        /// Cannot be <c>null</c>, but can be an empty string.</param>
        /// <exception cref="ArgumentNullException"><paramref name="value"/>
        /// is <c>null</c>.</exception>
        public void AddDefaultValue(string value)
        {
            // Validate input.
            if (value == null) {
                throw new ArgumentNullException("value");
            }

            InternalAddValue(null, value);
        }

        /// <summary>
        /// Internal method that adds a registry value. Called by all public methods
        /// that can add values to the key.
        /// </summary>
        /// <param name="name">Name of the registry value. If <c>null</c> or set
        /// to an empty string, the default registry value will be used.</param>
        /// <param name="value">Value to set for the registry value. Cannot be
        /// <c>null</c>.</param>
        private void InternalAddValue(string name, string value)
        {
            Debug.Assert(value != null);

            string trueName;
            if (!String.IsNullOrEmpty(name)) {
                trueName = String.Format("\"{0}\"", name);
            } else {
                trueName = "@";
            }
            writer.WriteLine("{0}=\"{1}\"", trueName, value);
        }

        /// <summary>
        /// Private dispose method called by both the finalizer and the public
        /// <see cref="Dispose()"/> method. We take care of finalizing the key
        /// and calling back our parent.
        /// </summary>
        /// <param name="disposing">Whether this is called from the <see cref="Dispose()"/>
        /// method (<c>true</c>) or the finalizer (<c>false</c>).</param>
        private void Dispose(bool disposing)
        {
            // Nothing to do to actually finalize the key; next key writer will
            // add another newline.

            if (parent != null) {
                // Clear reference to ourselves in parent so that it can add other keys.
                Debug.Assert(parent.keyWriter == this);
                parent.keyWriter = null;
                parent = null;
            }
            writer = null;
        }
    }

    /// <summary>
    /// Base class for all exceptions thrown by <see cref="RegistryFileWriter"/>
    /// and related classes.
    /// </summary>
    [Serializable]
    public class RegistryFileWriterException : CLRegAsmException
    {
        /// <summary>
        /// Constructor with no other argument.
        /// </summary>
        public RegistryFileWriterException()
            : base()
        {
        }

        /// <summary>
        /// Constructor with exception message.
        /// </summary>
        /// <param name="message">Exception message.</param>
        public RegistryFileWriterException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Constructor with message and inner exception.
        /// </summary>
        /// <param name="message">Exception message.</param>
        /// <param name="innerException">Inner exception, e.g. the exception that
        /// caused this exception to be thrown in the first place.</param>
        public RegistryFileWriterException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Exception thrown when the user calls <see cref="RegistryFileWriter.AddKey(string)"/>
    /// or <see cref="RegistryFileWriter.AddEmptyKey(string)"/> while there
    /// is already an active <see cref="RegistryKeyWriter"/> that should've
    /// been disposed of.
    /// </summary>
    [Serializable]
    public class ActiveRegistryKeyWriterException : RegistryFileWriterException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public ActiveRegistryKeyWriterException()
            : base("Cannot add new registry key while one is still active; dispose of the RegistryKeyWriter first")
        {
        }
    }
}
