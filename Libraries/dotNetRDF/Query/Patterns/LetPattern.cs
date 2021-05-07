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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VDS.RDF.Query.Algebra;
using VDS.RDF.Query.Expressions;

namespace VDS.RDF.Query.Patterns
{
    /// <summary>
    /// Class for representing LET Patterns in SPARQL Queries.
    /// </summary>
    public class LetPattern
        : BaseTriplePattern, IComparable<LetPattern>, IAssignmentPattern
    {
        private readonly string _var;
        private readonly ISparqlExpression _expr;

        /// <summary>
        /// Creates a new LET Pattern.
        /// </summary>
        /// <param name="var">Variable to assign to.</param>
        /// <param name="expr">Expression which generates a value which will be assigned to the variable.</param>
        public LetPattern(string var, ISparqlExpression expr)
        {
            _var = var;
            _expr = expr;
            _vars = _var.AsEnumerable().Concat(_expr.Variables).Distinct().ToList();
            _vars.Sort();
        }

        /// <summary>
        /// Evaluates a LET assignment in the given Evaluation Context.
        /// </summary>
        /// <param name="context">Evaluation Context.</param>
        public override void Evaluate(SparqlEvaluationContext context)
        {
            if (context.InputMultiset is NullMultiset)
            {
                context.OutputMultiset = context.InputMultiset;
            }
            else if (context.InputMultiset is IdentityMultiset)
            {
                var s = new Set();
                try
                {
                    INode temp = _expr.Evaluate(context, 0);
                    s.Add(_var, temp);
                    context.OutputMultiset.Add(s);
                }
                catch
                {
                    // No assignment if there's an error
                }
            }
            else
            {
                foreach (var id in context.InputMultiset.SetIDs.ToList())
                {
                    ISet s = context.InputMultiset[id];
                    if (s.ContainsVariable(_var))
                    {
                        try
                        {
                            // A value already exists so see if the two values match
                            INode current = s[_var];
                            INode temp = _expr.Evaluate(context, id);
                            if (current != temp)
                            {
                                // Where the values aren't equal the solution is eliminated
                                context.InputMultiset.Remove(id);
                            }
                        }
                        catch
                        {
                            // If an error occurs the solution is eliminated
                            context.InputMultiset.Remove(id);
                        }
                    }
                    else
                    {
                        context.InputMultiset.AddVariable(_var);
                        try
                        {
                            // Make a new assignment
                            INode temp = _expr.Evaluate(context, id);
                            s.Add(_var, temp);
                        }
                        catch
                        {
                            // If an error occurs no assignment happens
                        }
                    }
                }
                context.OutputMultiset = new IdentityMultiset();
            }
        }

        /// <summary>
        /// Gets the Pattern Type.
        /// </summary>
        public override TriplePatternType PatternType
        {
            get
            {
                return TriplePatternType.LetAssignment;
            }
        }

        /// <summary>
        /// Returns that this is not an accept all since it is a LET assignment.
        /// </summary>
        public override bool IsAcceptAll
        {
            get 
            {
                return false; 
            }
        }

        /// <summary>
        /// Gets the Expression that is used to generate values to be assigned.
        /// </summary>
        public ISparqlExpression AssignExpression
        {
            get
            {
                return _expr;
            }
        }

        /// <summary>
        /// Gets the Name of the Variable to which values will be assigned.
        /// </summary>
        public string VariableName
        {
            get
            {
                return _var;
            }
        }

        /// <summary>
        /// Returns an empty enumeration as any evaluation error will result in an unbound value so we can't guarantee any variables are bound.
        /// </summary>
        public override IEnumerable<string> FixedVariables
        {
            get { return Enumerable.Empty<string>(); }
        }

        /// <summary>
        /// Returns the variable being assigned to as any evaluation error will result in an unbound value so we can't guarantee it is bound.
        /// </summary>
        public override IEnumerable<string> FloatingVariables
        {
            get { return _var.AsEnumerable(); }
        }

        /// <summary>
        /// Gets whether the Pattern uses the Default Dataset.
        /// </summary>
        public override bool UsesDefaultDataset
        {
            get
            {
                return _expr.UsesDefaultDataset();
            }
        }

        /// <summary>
        /// Returns true as a LET can never contain Blank Nodes.
        /// </summary>
        public override bool HasNoBlankVariables
        {
            get 
            {
                return true;
            }
        }

        /// <summary>
        /// Gets the string representation of the LET assignment.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            var output = new StringBuilder();
            output.Append("LET(");
            output.Append("?");
            output.Append(_var);
            output.Append(" := ");
            output.Append(_expr.ToString());
            output.Append(")");

            return output.ToString();
        }

        /// <summary>
        /// Compares this Let to another Let.
        /// </summary>
        /// <param name="other">Let to compare to.</param>
        /// <returns>Just calls the base compare method since that implements all the logic we need.</returns>
        public int CompareTo(LetPattern other)
        {
            return base.CompareTo(other);
        }

        /// <summary>
        /// Compares this Let to another Let.
        /// </summary>
        /// <param name="other">Let to compare to.</param>
        /// <returns>Just calls the base compare method since that implements all the logic we need.</returns>
        public int CompareTo(IAssignmentPattern other)
        {
            return base.CompareTo(other);
        }
    }
}
