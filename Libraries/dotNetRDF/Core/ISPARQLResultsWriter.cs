/*
// <copyright>
// dotNetRDF is free and open source software licensed under the MIT License
// -------------------------------------------------------------------------
// 
// Copyright (c) 2009-2021 dotNetRDF Project (http://dotnetrdf.org/)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is furnished
// to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// </copyright>
*/

using System.IO;
using System.Text;
using VDS.RDF.Query;

namespace VDS.RDF
{
    /// <summary>
    /// Interface for Writer classes which serialize Sparql Result Sets into concrete results set syntaxes.
    /// </summary>
    public interface ISparqlResultsWriter
    {
        /// <summary>
        /// Saves the Result Set to the given file using UTF-8 text encoding with no byte-order mark.
        /// </summary>
        /// <param name="results">Result Set to save.</param>
        /// <param name="filename">File to save to.</param>
        void Save(SparqlResultSet results, string filename);

        /// <summary>
        /// Saves the result set to the specified file using the specified text encoding.
        /// </summary>
        /// <param name="results">The results set to save.</param>
        /// <param name="filename">The path to the file to be written.</param>
        /// <param name="fileEncoding">The text encoding to use.</param>
        void Save(SparqlResultSet results, string filename, Encoding fileEncoding);

        /// <summary>
        /// Saves the Result Set to the given Stream.
        /// </summary>
        /// <param name="results">Result Set to save.</param>
        /// <param name="output">Stream to save to.</param>
        void Save(SparqlResultSet results, TextWriter output);

        /// <summary>
        /// Event raised when a non-fatal issue with the SPARQL Results being written is detected
        /// </summary>
        event SparqlWarning Warning;
    }
}
