using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ScrapFishing.Gacha
{
    // JsonUtility가 못 읽는 키-값 객체(등급별 장비, 효과 키)를 읽기 위한 작은 파서.
    // 객체는 Dictionary<string, object>, 배열은 List<object>, 숫자는 double, 나머지는 string·bool·null이다.
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            var reader = new Reader(json);
            var value = reader.Value();
            reader.SkipSpace();
            if (!reader.AtEnd)
            {
                throw new FormatException("JSON 뒤에 남은 문자가 있습니다: " + reader.Position);
            }

            return value;
        }

        sealed class Reader
        {
            readonly string _text;
            int _at;

            public Reader(string text)
            {
                _text = text;
            }

            public bool AtEnd => _at >= _text.Length;
            public int Position => _at;

            public object Value()
            {
                SkipSpace();
                if (AtEnd)
                {
                    throw new FormatException("JSON이 끝났습니다.");
                }

                var c = _text[_at];
                switch (c)
                {
                    case '{': return Object();
                    case '[': return Array();
                    case '"': return String();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default: return Number();
                }
            }

            Dictionary<string, object> Object()
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                _at++;
                SkipSpace();
                if (Peek('}'))
                {
                    _at++;
                    return result;
                }

                while (true)
                {
                    SkipSpace();
                    var key = String();
                    SkipSpace();
                    Take(':');
                    result[key] = Value();
                    SkipSpace();
                    if (Peek(','))
                    {
                        _at++;
                        continue;
                    }

                    Take('}');
                    return result;
                }
            }

            List<object> Array()
            {
                var result = new List<object>();
                _at++;
                SkipSpace();
                if (Peek(']'))
                {
                    _at++;
                    return result;
                }

                while (true)
                {
                    result.Add(Value());
                    SkipSpace();
                    if (Peek(','))
                    {
                        _at++;
                        continue;
                    }

                    Take(']');
                    return result;
                }
            }

            string String()
            {
                Take('"');
                var builder = new StringBuilder();
                while (!AtEnd)
                {
                    var c = _text[_at++];
                    if (c == '"')
                    {
                        return builder.ToString();
                    }

                    if (c != '\\')
                    {
                        builder.Append(c);
                        continue;
                    }

                    var escape = _text[_at++];
                    switch (escape)
                    {
                        case 'n': builder.Append('\n'); break;
                        case 't': builder.Append('\t'); break;
                        case 'r': builder.Append('\r'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'u':
                            builder.Append((char)int.Parse(_text.Substring(_at, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _at += 4;
                            break;
                        default: builder.Append(escape); break;
                    }
                }

                throw new FormatException("문자열이 닫히지 않았습니다.");
            }

            double Number()
            {
                var start = _at;
                while (!AtEnd && "+-0123456789.eE".IndexOf(_text[_at]) >= 0)
                {
                    _at++;
                }

                if (start == _at)
                {
                    throw new FormatException("알 수 없는 값: " + _text[_at] + " @" + _at);
                }

                return double.Parse(_text.Substring(start, _at - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            public void SkipSpace()
            {
                while (!AtEnd && char.IsWhiteSpace(_text[_at]))
                {
                    _at++;
                }
            }

            bool Peek(char c)
            {
                return !AtEnd && _text[_at] == c;
            }

            void Take(char c)
            {
                if (!Peek(c))
                {
                    throw new FormatException("'" + c + "'가 필요합니다 @" + _at);
                }

                _at++;
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(_text, _at, word, 0, word.Length) != 0)
                {
                    throw new FormatException(word + "가 필요합니다 @" + _at);
                }

                _at += word.Length;
            }
        }
    }
}
