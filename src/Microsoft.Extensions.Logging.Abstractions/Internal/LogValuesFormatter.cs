// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;

namespace Microsoft.Extensions.Logging.Internal
{
    /// <summary>
    /// Formatter to convert the named format items like {NamedformatItem} to <see cref="M:string.Format"/> format.
    /// </summary>
    public class LogValuesFormatter
    {
        private const string NullValue = "(null)";
        private static readonly object[] EmptyArray = new object[0];
        private static readonly char[] FormatDelimiters = { ',', ':' };
        private string _format;
        private List<string> _valueNames;
        private List<string> _emptyValueNames = new List<string>(0);

        public LogValuesFormatter(string format)
        {
            OriginalFormat = format;
        }

        public string OriginalFormat { get; private set; }
        public List<string> ValueNames
        {
            get
            {
                if (_format == null) FormatInput(OriginalFormat); //construct the valuenames from the input
                return _valueNames ?? _emptyValueNames;
            }
        }

        private string FormatInput(string format)
        {
            if (_format != null)
            {
                return _format;
            }
            var scanIndex = 0;
            var endIndex = format.Length;
            char openBrace = '{', closingBrace = '}';

            //{} => Nothing to process, min length should be greater than 2. Ex: "{0}"
            if (format.Length < 3) 
            {
                _format = format;
                return format;
            }
            var sb = new StringBuilder();
            _valueNames = new List<string>();
            while (scanIndex < endIndex)
            {
                var openBraceIndex = FindBraceIndex(format, openBrace, scanIndex, endIndex);
                var closeBraceIndex = FindBraceIndex(format, closingBrace, openBraceIndex, endIndex);

                if (closeBraceIndex == endIndex)
                {
                    sb.Append(format, scanIndex, endIndex - scanIndex);
                    scanIndex = endIndex;
                }
                else
                {
                    sb.Append(format, scanIndex, openBraceIndex - scanIndex + 1);
                    sb.Append(_valueNames.Count.ToString(CultureInfo.InvariantCulture));
                    // Format item syntax : { index[,alignment][ :formatString] }.
                    var formatDelimiterIndex = FindIndexOfAny(format, FormatDelimiters, openBraceIndex, closeBraceIndex);
                    _valueNames.Add(format.Substring(openBraceIndex + 1, formatDelimiterIndex - openBraceIndex - 1));
                    sb.Append(format, formatDelimiterIndex, closeBraceIndex - formatDelimiterIndex + 1);

                    scanIndex = closeBraceIndex + 1;
                }
            }
            _format = sb.ToString();
            return _format;
        }

        private static int FindBraceIndex(string format, char brace, int startIndex, int endIndex)
        {
            // Example: {{prefix{{{Argument}}}suffix}}.
            var braceIndex = endIndex;
            var scanIndex = startIndex;
            var braceOccurenceCount = 0;

            while (scanIndex < endIndex)
            {
                if (braceOccurenceCount > 0 && format[scanIndex] != brace)
                {
                    if (braceOccurenceCount % 2 == 0)
                    {
                        // Even number of '{' or '}' found. Proceed search with next occurence of '{' or '}'.
                        braceOccurenceCount = 0;
                        braceIndex = endIndex;
                    }
                    else
                    {
                        // An unescaped '{' or '}' found.
                        break;
                    }
                }
                else if (format[scanIndex] == brace)
                {
                    if (brace == '}')
                    {
                        if (braceOccurenceCount == 0)
                        {
                            // For '}' pick the first occurence.
                            braceIndex = scanIndex;
                        }
                    }
                    else
                    {
                        // For '{' pick the last occurence.
                        braceIndex = scanIndex;
                    }

                    braceOccurenceCount++;
                }

                scanIndex++;
            }

            return braceIndex;
        }

        private static int FindIndexOfAny(string format, char[] chars, int startIndex, int endIndex)
        {
            var findIndex = format.IndexOfAny(chars, startIndex, endIndex - startIndex);
            return findIndex == -1 ? endIndex : findIndex;
        }

        public string Format(object[] values)
        {
            if (values != null)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    var value = values[i];

                    if (value == null)
                    {
                        values[i] = NullValue;
                        continue;
                    }

                    // since 'string' implements IEnumerable, special case it
                    if (value is string)
                    {
                        continue;
                    }

                    // if the value implements IEnumerable, build a comma separated string.
                    var enumerable = value as IEnumerable;
                    if (enumerable != null)
                    {
                        values[i] = string.Join(", ", enumerable.Cast<object>().Select(o => o ?? NullValue));
                    }
                }
            }

            return string.Format(CultureInfo.InvariantCulture, FormatInput(OriginalFormat), values ?? EmptyArray);
        }

        public KeyValuePair<string, object> GetValue(object[] values, int index)
        {
            if (_format == null) FormatInput(OriginalFormat);
            if (index < 0 || index > _valueNames?.Count)
            {
                throw new IndexOutOfRangeException(nameof(index));
            }

            if (_valueNames?.Count > index)
            {
                return new KeyValuePair<string, object>(_valueNames[index], values[index]);
            }

            return new KeyValuePair<string, object>("{OriginalFormat}", OriginalFormat);
        }

        public IEnumerable<KeyValuePair<string, object>> GetValues(object[] values)
        {
            var valueArray = new KeyValuePair<string, object>[values.Length + 1];

            if (_format == null) FormatInput(OriginalFormat);
            if (_valueNames != null)
            {
                for (var index = 0; index != _valueNames.Count; ++index)
                {
                    valueArray[index] = new KeyValuePair<string, object>(_valueNames[index], values[index]);
                } 
            }

            valueArray[valueArray.Length - 1] = new KeyValuePair<string, object>("{OriginalFormat}", OriginalFormat);
            return valueArray;
        }
    }
}
