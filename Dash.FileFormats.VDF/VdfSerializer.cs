﻿// 
// This file is licensed under the terms of the Simple Non Code License (SNCL) 2.1.0.
// The full license text can be found in the file named License.txt.
// Written originally by Alexandre Quoniou in 2017.
//

using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;

namespace Dash.FileFormats.VDF
{
    public static class VdfSerializer
    {
        private static readonly char[] Whitespaces = { ' ', '\r', '\n', '\t' };
        private static readonly Dictionary<char, char> EscapeSequences = new Dictionary<char, char>()
        {
            { 'n', '\n' },
            { 't', '\t' },
            { '"', '"' },
            { '\\', '\\' }
        };

        public static dynamic Deserialize(string data)
        {
            IDictionary<string, object> keyValues = new ExpandoObject();

            var states = new Stack<DeserializationStates>();
            states.Push(DeserializationStates.ExpectKey);
            states.Push(DeserializationStates.IgnoreWhitespaces);

            var stack = new Stack<IDictionary<string, object>>();
            stack.Push(keyValues);

            string currentKey = null;
            StringBuilder currentToken = null;
            bool currentTokenQuoted = false;

            foreach (var c in data)
            {
                var currentState = states.Peek();
                var currentKeyValues = stack.Peek();

                switch (currentState)
                {
                    case DeserializationStates.IgnoreWhitespaces:
                        if (c == '/')
                        {
                            states.Push(DeserializationStates.ExpectCommentSequence);
                        }
                        else if (!Whitespaces.Contains(c))
                        {
                            states.Pop();
                        }
                        break;
                    case DeserializationStates.ExpectCommentSequence:
                        if (c == '/')
                        {
                            states.Pop();
                            states.Push(DeserializationStates.ReadSinglelineComment);
                        }
                        else if (c == '*')
                        {
                            states.Pop();
                            states.Push(DeserializationStates.ReadMultilineComment);
                        }
                        else
                        {
                            throw new UnexpectedCharacterException();
                        }
                        break;
                    case DeserializationStates.ReadSinglelineComment:
                        if (c == '\n')
                        {
                            states.Pop();
                        }
                        break;
                    case DeserializationStates.ReadMultilineComment:
                        if (c == '*')
                        {
                            states.Push(DeserializationStates.ExpectMultilineCommentSequenceEnd);
                        }
                        break;
                    case DeserializationStates.ExpectMultilineCommentSequenceEnd:
                        if (c == '/')
                        {
                            states.Pop();
                            states.Pop();
                        }
                        break;
                }

                currentState = states.Peek();

                switch (currentState)
                {
                    case DeserializationStates.ExpectKey:
                        if (c == '"')
                        {
                            currentToken = new StringBuilder();
                            currentTokenQuoted = true;

                            states.Pop();
                            states.Push(DeserializationStates.ReadKey);
                        }
                        else if (c == '}')
                        {
                            stack.Pop();
                            states.Push(DeserializationStates.IgnoreWhitespaces);
                        }
                        else if (!Whitespaces.Contains(c))
                        {
                            currentToken = new StringBuilder();
                            currentTokenQuoted = false;

                            states.Pop();
                            states.Push(DeserializationStates.ReadKey);
                        }
                        else
                        {
                            throw new UnexpectedCharacterException();
                        }
                        break;
                    case DeserializationStates.ExpectValue:
                        if (c == '"')
                        {
                            currentToken = new StringBuilder();
                            currentTokenQuoted = true;

                            states.Pop();
                            states.Push(DeserializationStates.ReadValue);
                        }
                        else if (c == '{')
                        {
                            IDictionary<string, object> subKeyValues = new ExpandoObject();
                            currentKeyValues.Add(currentKey, subKeyValues);

                            stack.Push(subKeyValues);

                            states.Pop();
                            states.Push(DeserializationStates.ExpectKey);
                            states.Push(DeserializationStates.IgnoreWhitespaces);
                        }
                        else if (!Whitespaces.Contains(c))
                        {
                            currentToken = new StringBuilder();
                            currentTokenQuoted = false;

                            states.Pop();
                            states.Push(DeserializationStates.ReadValue);
                        }
                        else
                        {
                            throw new UnexpectedCharacterException();
                        }
                        break;
                    case DeserializationStates.ReadKey when (currentTokenQuoted && c == '"') || (!currentTokenQuoted && Whitespaces.Contains(c)):
                        currentKey = currentToken.ToString();
                        states.Pop();
                        states.Push(DeserializationStates.ExpectValue);
                        states.Push(DeserializationStates.IgnoreWhitespaces);
                        break;
                    case DeserializationStates.ReadValue when currentTokenQuoted && c == '"' || (!currentTokenQuoted && Whitespaces.Contains(c)):
                        currentKeyValues.Add(currentKey, currentToken.ToString());
                        states.Pop();
                        states.Push(DeserializationStates.ExpectKey);
                        states.Push(DeserializationStates.IgnoreWhitespaces);
                        break;
                    case DeserializationStates.ReadKey when c == '\\':
                    case DeserializationStates.ReadValue when c == '\\':
                        states.Push(DeserializationStates.ExpectEscapeSequence);
                        break;
                    case DeserializationStates.ReadKey:
                    case DeserializationStates.ReadValue:
                        currentToken.Append(c);
                        break;
                    case DeserializationStates.ExpectEscapeSequence:
                        if (EscapeSequences.ContainsKey(c))
                        {
                            currentToken.Append(EscapeSequences[c]);
                            states.Pop();
                        }
                        else
                        {
                            throw new UnexpectedCharacterException();
                        }
                        break;
                }
            }

            return keyValues;
        }

        private enum DeserializationStates
        {
            ExpectKey,
            ReadKey,
            ExpectValue,
            ReadValue,
            ExpectEscapeSequence,
            IgnoreWhitespaces,
            ExpectCommentSequence,
            ReadSinglelineComment,
            ReadMultilineComment,
            ExpectMultilineCommentSequenceEnd
        }
    }
}
